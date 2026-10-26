using System.Globalization;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Items, categories, BOM, price lists, pricing, units, taxes, currencies and Excel import (ITM, ADM-12/13/14/23).</summary>
public static class CatalogEndpoints
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var items = api.MapGroup("/items").WithTags("Items").RequireAuthorization(PermissionPolicy.For(Permissions.Items.Read));

        items.MapGet("/", (int? page, int? pageSize, string? search, string? sort, string? type, Guid? categoryId, bool? active,
                ItemService svc, CancellationToken ct) =>
                svc.ListAsync(new PageQuery(page ?? 1, pageSize ?? 20, search, sort), new ItemFilter(type, categoryId, active), ct))
            .WithSummary("List items and services (standard cost is null without inventory.valuation.read)");

        items.MapGet("/options", (string? search, string? kind, ItemService svc, CancellationToken ct) => svc.OptionsAsync(search, kind, ct))
            .WithSummary("Active items for pickers; kind = service | stock");

        items.MapGet("/{id:guid}", (Guid id, ItemService svc, CancellationToken ct) => svc.GetAsync(id, ct)).WithName("GetItem");

        items.MapPost("/", async (ItemUpsertRequest request, ItemService svc, CancellationToken ct) =>
            {
                var created = await svc.CreateAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetItem", new { id = created.Id });
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.Manage))
            .WithSummary("Create an item; an empty SKU takes the next number (ITM-00001)");

        items.MapPut("/{id:guid}", (Guid id, ItemUpsertRequest request, ItemService svc, CancellationToken ct) => svc.UpdateAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.Manage));

        items.MapGet("/{id:guid}/bom", (Guid id, ItemService svc, CancellationToken ct) => svc.ListBomAsync(id, ct))
            .WithSummary("Standard material consumption of a service");
        items.MapPost("/{id:guid}/bom", (Guid id, BomLineRequest request, ItemService svc, CancellationToken ct) => svc.SaveBomLineAsync(id, null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.BomManage));
        items.MapPut("/{id:guid}/bom/{lineId:guid}", (Guid id, Guid lineId, BomLineRequest request, ItemService svc, CancellationToken ct) =>
                svc.SaveBomLineAsync(id, lineId, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.BomManage));
        items.MapDelete("/{id:guid}/bom/{lineId:guid}", async (Guid id, Guid lineId, ItemService svc, CancellationToken ct) =>
            {
                await svc.DeleteBomLineAsync(id, lineId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.BomManage));

        items.MapGet("/{id:guid}/conversions", (Guid id, ItemService svc, CancellationToken ct) => svc.ListConversionsAsync(id, ct));
        items.MapPost("/{id:guid}/conversions", async (Guid id, UnitConversionRequest request, ItemService svc, CancellationToken ct) =>
            {
                await svc.AddConversionAsync(id, request, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.Manage));
        items.MapDelete("/{id:guid}/conversions/{conversionId:guid}", async (Guid id, Guid conversionId, ItemService svc, CancellationToken ct) =>
            {
                await svc.DeleteConversionAsync(id, conversionId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.Manage));

        items.MapGet("/{id:guid}/suppliers", (Guid id, SupplierService svc, CancellationToken ct) => svc.ListForItemAsync(id, ct));

        var categories = api.MapGroup("/item-categories").WithTags("Items").RequireAuthorization(PermissionPolicy.For(Permissions.Items.Read));
        categories.MapGet("/", (ItemService svc, CancellationToken ct) => svc.ListCategoriesAsync(ct));
        categories.MapPost("/", (ItemCategoryUpsertRequest request, ItemService svc, CancellationToken ct) => svc.SaveCategoryAsync(null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.CategoriesManage));
        categories.MapPut("/{id:guid}", (Guid id, ItemCategoryUpsertRequest request, ItemService svc, CancellationToken ct) => svc.SaveCategoryAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.CategoriesManage));
        categories.MapDelete("/{id:guid}", async (Guid id, ItemService svc, CancellationToken ct) =>
            {
                await svc.DeleteCategoryAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Items.CategoriesManage));

        var lists = api.MapGroup("/price-lists").WithTags("Pricing").RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Read));
        lists.MapGet("/", (PriceListService svc, CancellationToken ct) => svc.ListAsync(ct));
        lists.MapGet("/{id:guid}", (Guid id, PriceListService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        lists.MapPost("/", (PriceListUpsertRequest request, PriceListService svc, CancellationToken ct) => svc.SaveAsync(null, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage));
        lists.MapPut("/{id:guid}", (Guid id, PriceListUpsertRequest request, PriceListService svc, CancellationToken ct) => svc.SaveAsync(id, request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage))
            .WithSummary("Update a list; making it the default moves the flag from the previous default");
        lists.MapGet("/{id:guid}/lines", (Guid id, int? page, int? pageSize, string? search, PriceListService svc, CancellationToken ct) =>
            svc.ListLinesAsync(id, new PageQuery(page ?? 1, pageSize ?? 50, search), ct));
        lists.MapPut("/{id:guid}/lines", async (Guid id, PriceListLineRequest request, PriceListService svc, CancellationToken ct) =>
            {
                await svc.SetLineAsync(id, request, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage))
            .WithSummary("Set an item's price for a minimum quantity (adds or replaces)");
        lists.MapDelete("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, PriceListService svc, CancellationToken ct) =>
            {
                await svc.DeleteLineAsync(id, lineId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Manage));
        lists.MapPost("/bulk-update/preview", (BulkPriceUpdateRequest request, PriceListService svc, CancellationToken ct) => svc.PreviewBulkAsync(request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Bulk))
            .WithSummary("Preview a percentage change; nothing is saved");
        lists.MapPost("/bulk-update", async (BulkPriceUpdateRequest request, PriceListService svc, CancellationToken ct) =>
                TypedResults.Ok(new { updated = await svc.ApplyBulkAsync(request, ct) }))
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Bulk));

        api.MapGet("/pricing/quote", (Guid customerId, Guid itemId, decimal? quantity, DateOnly? date, PriceListService svc, CancellationToken ct) =>
                svc.QuoteAsync(customerId, itemId, quantity ?? 1, date, ct))
            .WithTags("Pricing")
            .RequireAuthorization(PermissionPolicy.For(Permissions.PriceLists.Read))
            .WithSummary("Resolve a customer's price: special price, then customer/group list, then default list, then item price");

        MapReferenceData(api);
        MapImport(api);
        return api;
    }

    private static void MapReferenceData(RouteGroupBuilder api)
    {
        var units = api.MapGroup("/units").WithTags("Reference data");
        units.MapGet("/", (ReferenceDataService svc, CancellationToken ct) => svc.ListUnitsAsync(ct)).RequireAuthorization();
        units.MapPost("/", async (UnitUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveUnitAsync(null, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.UnitsManage));
        units.MapPut("/{id:guid}", async (Guid id, UnitUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveUnitAsync(id, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.UnitsManage));
        units.MapDelete("/{id:guid}", async (Guid id, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.DeleteUnitAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.UnitsManage));

        var taxes = api.MapGroup("/tax-codes").WithTags("Reference data");
        taxes.MapGet("/", (bool? activeOnly, ReferenceDataService svc, CancellationToken ct) => svc.ListTaxCodesAsync(activeOnly ?? false, ct)).RequireAuthorization();
        taxes.MapPost("/", async (TaxCodeUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveTaxCodeAsync(null, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.TaxesManage));
        taxes.MapPut("/{id:guid}", async (Guid id, TaxCodeUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveTaxCodeAsync(id, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.TaxesManage));

        var currencies = api.MapGroup("/currencies").WithTags("Reference data");
        currencies.MapGet("/", (ReferenceDataService svc, CancellationToken ct) => svc.ListCurrenciesAsync(ct)).RequireAuthorization();
        currencies.MapPost("/", async (CurrencyUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveCurrencyAsync(null, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.CurrenciesManage));
        currencies.MapPut("/{id:guid}", async (Guid id, CurrencyUpsertRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SaveCurrencyAsync(id, r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.CurrenciesManage));
        currencies.MapGet("/rates", (int? take, ReferenceDataService svc, CancellationToken ct) => svc.ListRatesAsync(take ?? 100, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.CurrenciesManage));
        currencies.MapPut("/rates", async (ExchangeRateRequest r, ReferenceDataService svc, CancellationToken ct) =>
            {
                await svc.SetRateAsync(r, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Admin.CurrenciesManage))
            .WithSummary("Record the rate for a pair and day (replaces that day's earlier rate)");
    }

    private static void MapImport(RouteGroupBuilder api)
    {
        var import = api.MapGroup("/import").WithTags("Import").RequireAuthorization();

        import.MapGet("/{kind}/template", async (string kind, ImportService svc, CancellationToken ct) =>
                TypedResults.File(await svc.TemplateAsync(KindOf(kind), CultureInfo.CurrentUICulture.Name, ct), XlsxContentType,
                    $"{KindOf(kind).ToString().ToLowerInvariant()}-template.xlsx"))
            .WithSummary("Download the Excel template (headers in the request language)");

        import.MapPost("/{kind}/preview", async (string kind, IFormFile file, ImportService svc, CancellationToken ct) =>
            {
                var importKind = KindOf(kind);
                await using var stream = Open(file);
                return await svc.PreviewAsync(importKind, stream, ct);
            })
            .DisableAntiforgery()
            .WithSummary("Validate a workbook; nothing is saved");

        import.MapPost("/{kind}", async (string kind, IFormFile file, ImportService svc, CancellationToken ct) =>
            {
                var importKind = KindOf(kind);
                await using var stream = Open(file);
                return await svc.ImportAsync(importKind, stream, ct);
            })
            .DisableAntiforgery()
            .WithSummary("Import every row in one transaction, or nothing if any row is invalid");
    }

    /// <summary>"customers" or "items", in any letter case.</summary>
    private static ImportKind KindOf(string kind) =>
        Application.Common.MasterData.TryParseEnum<ImportKind>(kind, out var k) ? k : throw new Application.Common.NotFoundException("ImportKind");

    private static Stream Open(IFormFile file) =>
        file.Length is > 0 and <= ImportService.MaxFileBytes
            ? file.OpenReadStream()
            : throw Application.Common.ValidationException.For("File", "Import.FileSize");
}
