using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Suppliers;

[Screen("SUP-05")]
[HasPermission(Permissions.Suppliers.ItemsManage)]
public sealed class ItemsModel(SupplierService suppliers, ItemService items, ReferenceDataService reference) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public SupplierDto Supplier { get; private set; } = default!;
    public IReadOnlyList<SupplierItemDto> Lines { get; private set; } = [];
    public IReadOnlyList<OptionDto> ItemOptions { get; private set; } = [];
    public IReadOnlyList<CurrencyDto> Currencies { get; private set; } = [];

    public sealed class InputModel
    {
        public Guid ItemId { get; set; }
        public string? SupplierSku { get; set; }
        public decimal LastPrice { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public int LeadTimeDays { get; set; }
        public decimal MinOrderQty { get; set; }
        public bool IsPreferred { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input.CurrencyCode = Currencies.FirstOrDefault(c => c.IsBase)?.Code ?? string.Empty;
        if (Edit is { } id && Lines.FirstOrDefault(l => l.Id == id) is { } l)
        {
            Input = new InputModel
            {
                ItemId = l.ItemId, SupplierSku = l.SupplierSku, LastPrice = l.LastPrice ?? 0, CurrencyCode = l.CurrencyCode,
                LeadTimeDays = l.LeadTimeDays, MinOrderQty = l.MinOrderQty, IsPreferred = l.IsPreferred,
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new SupplierItemUpsertRequest(Input.ItemId, Input.SupplierSku, Input.LastPrice, Input.CurrencyCode, Input.LeadTimeDays,
            Input.MinOrderQty, Input.IsPreferred);
        if (!await TryExecuteAsync(() => suppliers.SaveItemAsync(Id, Edit, request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/suppliers/{Id}/items");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid lineId)
    {
        await suppliers.DeleteItemAsync(Id, lineId);
        Flash("Common.Deleted");
        return Redirect($"/suppliers/{Id}/items");
    }

    private async Task LoadAsync()
    {
        Supplier = await suppliers.GetAsync(Id);
        Lines = await suppliers.ListItemsAsync(Id);
        ItemOptions = Can(Permissions.Items.Read) ? await items.OptionsAsync(null) : [];
        Currencies = await reference.ListCurrenciesAsync();
    }
}
