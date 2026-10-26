using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-06")]
[HasPermission(Permissions.Items.BomManage)]
public sealed class BomModel(ItemService items, ReferenceDataService reference) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public ItemDto Service { get; private set; } = default!;
    public IReadOnlyList<BomLineDto> Lines { get; private set; } = [];
    public IReadOnlyList<OptionDto> Materials { get; private set; } = [];
    public IReadOnlyList<UnitDto> Units { get; private set; } = [];

    public sealed class InputModel
    {
        public Guid MaterialItemId { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public decimal ScrapPercent { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Service.Type is not ("Service" or "OutsourcedService"))
        {
            return Redirect($"/items/{Id}");
        }

        if (Edit is { } id && Lines.FirstOrDefault(l => l.Id == id) is { } l)
        {
            Input = new InputModel { MaterialItemId = l.MaterialItemId, Quantity = l.Quantity, UnitId = l.UnitId, ScrapPercent = l.ScrapPercent };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new BomLineRequest(Input.MaterialItemId, Input.Quantity, Input.UnitId, Input.ScrapPercent);
        if (!await TryExecuteAsync(() => items.SaveBomLineAsync(Id, Edit, request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/items/{Id}/bom");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid lineId)
    {
        await items.DeleteBomLineAsync(Id, lineId);
        Flash("Common.Deleted");
        return Redirect($"/items/{Id}/bom");
    }

    private async Task LoadAsync()
    {
        Service = await items.GetAsync(Id);
        Lines = await items.ListBomAsync(Id);
        Materials = await items.OptionsAsync(null, "stock");
        Units = await reference.ListUnitsAsync();
    }
}
