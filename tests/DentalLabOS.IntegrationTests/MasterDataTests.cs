using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.IntegrationTests;

/// <summary>Phase 2: customers, suppliers, items, price lists and Excel import against the real engine.</summary>
[Collection(SqlCollection.Name)]
public sealed class MasterDataTests(SqlServerFixture fx)
{
    private static async Task<string?> FirstErrorKey(HttpResponseMessage response, string field)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("errors").GetProperty(field)[0].GetProperty("key").GetString();
    }

    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task Success(HttpResponseMessage response) =>
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private async Task<ItemDto> CreateServiceAsync(HttpClient client, string name, decimal price)
    {
        var categories = await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/v1/item-categories");
        var units = await client.GetFromJsonAsync<List<UnitDto>>("/api/v1/units");
        return await Ok<ItemDto>(await client.PostAsJsonAsync("/api/v1/items",
            new ItemUpsertRequest(null, name, "Service", categories![0].Id, units!.First(u => u.Code == "UNIT").Id, DefaultSalesPrice: price, StandardCost: 40)));
    }

    [SqlFact]
    public async Task New_lab_gets_starter_catalog()
    {
        var client = fx.Client(fx.LabB.Token);
        var units = await client.GetFromJsonAsync<List<UnitDto>>("/api/v1/units");
        Assert.Contains(units!, u => u.Code == "PCS");
        var lists = await client.GetFromJsonAsync<List<PriceListDto>>("/api/v1/price-lists");
        Assert.Single(lists!, l => l.IsDefault);
        Assert.NotEmpty((await client.GetFromJsonAsync<List<ItemCategoryDto>>("/api/v1/item-categories"))!);
        Assert.NotEmpty((await client.GetFromJsonAsync<List<CurrencyDto>>("/api/v1/currencies"))!);

        var sequences = await fx.QueryAsync(db => db.DocumentSequences.IgnoreQueryFilters()
            .Where(s => s.TenantId == fx.LabB.TenantId).Select(s => s.Prefix).ToListAsync());
        Assert.Contains("C", sequences);
        Assert.Contains("S", sequences);
        Assert.Contains("ITM", sequences);
    }

    [SqlFact]
    public async Task Customers_get_automatic_codes_and_codes_stay_unique()
    {
        var client = fx.Client(fx.LabA.Token);
        var first = await Ok<CustomerDto>(await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(null, "Smile Clinic", "Clinic")));
        Assert.Matches(@"^C-\d{5}$", first.Code);

        var duplicate = await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(first.Code.ToLowerInvariant(), "Copy", "Clinic"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Validation.CodeTaken", await FirstErrorKey(duplicate, "Code"));

        // A typed code that collides with the next number is skipped by the generator.
        var next = int.Parse(first.Code[2..]) + 1;
        await Ok<CustomerDto>(await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest($"C-{next:00000}", "Manual", "Dentist")));
        var auto = await Ok<CustomerDto>(await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(null, "Auto", "Center")));
        Assert.NotEqual($"C-{next:00000}", auto.Code);

        // Same code in another lab is fine: uniqueness is per tenant.
        await Ok<CustomerDto>(await fx.Client(fx.LabB.Token).PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(first.Code, "Other lab", "Clinic")));
    }

    [SqlFact]
    public async Task Labs_cannot_see_or_reference_each_others_master_data()
    {
        var a = fx.Client(fx.LabA.Token);
        var b = fx.Client(fx.LabB.Token);
        var customer = await Ok<CustomerDto>(await a.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(null, "Private Clinic A", "Clinic")));
        var item = await CreateServiceAsync(a, "Private crown A", 500);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/customers/{customer.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/items/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/v1/customers/{customer.Id}",
            new CustomerUpsertRequest(customer.Code, "Hacked", "Clinic", RowVersion: customer.RowVersion))).StatusCode);
        var list = await b.GetFromJsonAsync<PagedResult<CustomerListItemDto>>("/api/v1/customers?search=Private");
        Assert.Empty(list!.Items);

        // Lab B may not attach Lab A's item to its own price list.
        var bList = (await b.GetFromJsonAsync<List<PriceListDto>>("/api/v1/price-lists"))!.Single(l => l.IsDefault);
        var line = await b.PutAsJsonAsync($"/api/v1/price-lists/{bList.Id}/lines", new PriceListLineRequest(item.Id, 1));
        Assert.Equal(HttpStatusCode.BadRequest, line.StatusCode);
    }

    [SqlFact]
    public async Task Permissions_are_enforced_on_the_server_and_cost_is_hidden()
    {
        var technician = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Technician, "tech@lab-a.test"));
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync("/api/v1/suppliers", new SupplierUpsertRequest(null, "X"))).StatusCode);

        var owner = fx.Client(fx.LabA.Token);
        var item = await CreateServiceAsync(owner, "Costed veneer", 300);
        Assert.Equal(40, item.StandardCost);

        var sales = fx.Client(await fx.AddUserAsync(fx.LabA, SystemRoles.Sales, "sales@lab-a.test"));
        var seen = await sales.GetFromJsonAsync<ItemDto>($"/api/v1/items/{item.Id}");
        Assert.Null(seen!.StandardCost);
        Assert.Equal(HttpStatusCode.Forbidden, (await sales.PostAsJsonAsync("/api/v1/items",
            new ItemUpsertRequest(null, "Nope", "Service", item.CategoryId, item.BaseUnitId))).StatusCode);
    }

    [SqlFact]
    public async Task Quote_follows_price_priority()
    {
        var client = fx.Client(fx.LabA.Token);
        var item = await CreateServiceAsync(client, "Quoted bridge", 1000);
        var customer = await Ok<CustomerDto>(await client.PostAsJsonAsync("/api/v1/customers", new CustomerUpsertRequest(null, "Quote Clinic", "Clinic")));
        async Task<PriceQuoteDto> Quote(decimal qty = 1) =>
            (await client.GetFromJsonAsync<PriceQuoteDto>($"/api/v1/pricing/quote?customerId={customer.Id}&itemId={item.Id}&quantity={qty}"))!;

        Assert.Equal(("ItemDefault", 1000m), ((await Quote()).Source, (await Quote()).Price));

        var general = (await client.GetFromJsonAsync<List<PriceListDto>>("/api/v1/price-lists"))!.Single(l => l.IsDefault);
        await Success(await client.PutAsJsonAsync($"/api/v1/price-lists/{general.Id}/lines", new PriceListLineRequest(item.Id, 900)));
        await Success(await client.PutAsJsonAsync($"/api/v1/price-lists/{general.Id}/lines", new PriceListLineRequest(item.Id, 800, 10)));
        Assert.Equal(900, (await Quote()).Price);
        Assert.Equal(800, (await Quote(12)).Price);

        var vip = await Ok<PriceListDto>(await client.PostAsJsonAsync("/api/v1/price-lists", new PriceListUpsertRequest("VIP quote list", general.CurrencyCode)));
        await Success(await client.PutAsJsonAsync($"/api/v1/price-lists/{vip.Id}/lines", new PriceListLineRequest(item.Id, 850)));
        var withList = await Ok<CustomerDto>(await client.PutAsJsonAsync($"/api/v1/customers/{customer.Id}",
            new CustomerUpsertRequest(customer.Code, customer.Name, "Clinic", PriceListId: vip.Id, RowVersion: customer.RowVersion)));
        Assert.Equal(("CustomerPriceList", 850m), ((await Quote()).Source, (await Quote()).Price));

        await Success(await client.PostAsJsonAsync($"/api/v1/customers/{withList.Id}/prices", new CustomerPriceUpsertRequest(item.Id, 700)));
        Assert.Equal(("CustomerPrice", 700m), ((await Quote()).Source, (await Quote()).Price));
    }

    [SqlFact]
    public async Task Only_one_default_price_list_and_bulk_update_previews_then_applies()
    {
        var client = fx.Client(fx.LabB.Token);
        var oldDefault = (await client.GetFromJsonAsync<List<PriceListDto>>("/api/v1/price-lists"))!.Single(l => l.IsDefault);
        var created = await Ok<PriceListDto>(await client.PostAsJsonAsync("/api/v1/price-lists",
            new PriceListUpsertRequest("New default", oldDefault.CurrencyCode, IsDefault: true)));
        var lists = (await client.GetFromJsonAsync<List<PriceListDto>>("/api/v1/price-lists"))!;
        Assert.Equal(created.Id, Assert.Single(lists, l => l.IsDefault).Id);

        var item = await CreateServiceAsync(client, "Bulk crown", 100);
        await Success(await client.PutAsJsonAsync($"/api/v1/price-lists/{created.Id}/lines", new PriceListLineRequest(item.Id, 200)));
        var request = new BulkPriceUpdateRequest(created.Id, 10);
        var preview = await Ok<BulkPricePreviewDto>(await client.PostAsJsonAsync("/api/v1/price-lists/bulk-update/preview", request));
        var change = Assert.Single(preview.Changes);
        Assert.Equal((200m, 220m), (change.OldPrice, change.NewPrice));

        var unchanged = await client.GetFromJsonAsync<PagedResult<PriceListLineDto>>($"/api/v1/price-lists/{created.Id}/lines");
        Assert.Equal(200, Assert.Single(unchanged!.Items).Price);

        (await client.PostAsJsonAsync("/api/v1/price-lists/bulk-update", request)).EnsureSuccessStatusCode();
        var applied = await client.GetFromJsonAsync<PagedResult<PriceListLineDto>>($"/api/v1/price-lists/{created.Id}/lines");
        Assert.Equal(220, Assert.Single(applied!.Items).Price);

        var zero = await client.PostAsJsonAsync("/api/v1/price-lists/bulk-update/preview", request with { Percent = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    private static ByteArrayContent Workbook(string[] headers, params string?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Data");
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var content = new ByteArrayContent(ms.ToArray());
        content.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return content;
    }

    private static MultipartFormDataContent Form(ByteArrayContent file) => new() { { file, "file", "data.xlsx" } };

    [SqlFact]
    public async Task Excel_import_is_all_or_nothing()
    {
        var client = fx.Client(fx.LabA.Token);
        var template = await client.GetAsync("/api/v1/import/customers/template");
        template.EnsureSuccessStatusCode();
        Assert.True((await template.Content.ReadAsByteArrayAsync()).Length > 0);

        string[] headers = ["الاسم", "النوع", "المجموعة", "الحد الائتماني"];
        var bad = Form(Workbook(headers, ["Import One", "عيادة", "Imported group", "1000"], ["Import Two", "Spaceship", null, "abc"]));
        var preview = await Ok<ImportPreviewDto>(await client.PostAsync("/api/v1/import/customers/preview", bad));
        Assert.False(preview.CanImport);
        Assert.Equal(2, preview.TotalRows);
        Assert.Contains(preview.Errors, e => e.Row == 3);

        var rejected = await client.PostAsync("/api/v1/import/customers",
            Form(Workbook(headers, ["Import One", "عيادة", "Imported group", "1000"], ["Import Two", "Spaceship", null, "abc"])));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var none = await client.GetFromJsonAsync<PagedResult<CustomerListItemDto>>("/api/v1/customers?search=Import%20One");
        Assert.Empty(none!.Items);

        var good = Form(Workbook(headers, ["Import One", "عيادة", "Imported group", "1000"], ["Import Two", "طبيب", "Imported group", "٢٥٠"]));
        var result = await Ok<ImportResultDto>(await client.PostAsync("/api/v1/import/customers", good));
        Assert.Equal(2, result.Created);
        var imported = await client.GetFromJsonAsync<PagedResult<CustomerListItemDto>>("/api/v1/customers?search=Import%20T");
        var two = Assert.Single(imported!.Items);
        Assert.Equal(("Dentist", "Imported group", 250m), (two.Type, two.GroupName, two.CreditLimit));
    }
}
