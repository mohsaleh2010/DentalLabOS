using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-02")]
[HasPermission(Permissions.Items.Manage)]
public sealed class EditModel(ItemService items, ReferenceDataService reference, DentalLabOS.Application.Production.WorkflowService workflows) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public ConversionInput Conversion { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public IReadOnlyList<ItemCategoryDto> Categories { get; private set; } = [];
    public IReadOnlyList<OptionDto> Workflows { get; private set; } = [];
    public IReadOnlyList<UnitDto> Units { get; private set; } = [];
    public IReadOnlyList<TaxCodeDto> TaxCodes { get; private set; } = [];
    public IReadOnlyList<UnitConversionDto> Conversions { get; private set; } = [];

    public sealed class InputModel
    {
        public string? Sku { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string Type { get; set; } = "Service";
        public Guid CategoryId { get; set; }
        public Guid BaseUnitId { get; set; }
        public bool TracksBatches { get; set; }
        public bool TracksExpiry { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal ReorderQty { get; set; }
        public decimal? StandardCost { get; set; }
        public decimal DefaultSalesPrice { get; set; }
        public Guid? TaxCodeId { get; set; }
        public decimal StandardLeadHours { get; set; }
        public decimal DifficultyFactor { get; set; } = 1;
        public string? Barcode { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? DefaultWorkflowId { get; set; }
        public string? RowVersion { get; set; }
    }

    public sealed class ConversionInput
    {
        public Guid FromUnitId { get; set; }
        public Guid ToUnitId { get; set; }
        public decimal Factor { get; set; }
    }

    public async Task OnGetAsync(string? type)
    {
        if (!IsNew)
        {
            var i = await items.GetAsync(Guid.Parse(Id));
            Input = new InputModel
            {
                Sku = i.Sku, Name = i.Name, NameEn = i.NameEn, Type = i.Type, CategoryId = i.CategoryId, BaseUnitId = i.BaseUnitId,
                TracksBatches = i.TracksBatches, TracksExpiry = i.TracksExpiry, ReorderLevel = i.ReorderLevel, ReorderQty = i.ReorderQty,
                StandardCost = i.StandardCost, DefaultSalesPrice = i.DefaultSalesPrice, TaxCodeId = i.TaxCodeId, StandardLeadHours = i.StandardLeadHours,
                DifficultyFactor = i.DifficultyFactor, Barcode = i.Barcode, IsActive = i.IsActive, RowVersion = i.RowVersion,
                DefaultWorkflowId = i.DefaultWorkflowId,
            };
        }
        else if (IndexModel.Types.Contains(type))
        {
            Input.Type = type!;
        }

        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var request = new ItemUpsertRequest(i.Sku, i.Name, i.Type, i.CategoryId, i.BaseUnitId, i.NameEn, i.TracksBatches, i.TracksExpiry, i.ReorderLevel,
            i.ReorderQty, Can(Permissions.Items.CostRead) ? i.StandardCost ?? 0 : null, i.DefaultSalesPrice, i.TaxCodeId, i.StandardLeadHours,
            i.DifficultyFactor, i.Barcode, i.IsActive, i.RowVersion, i.DefaultWorkflowId);
        ItemDto? saved = null;
        if (!await TryExecuteAsync(async () => saved = IsNew ? await items.CreateAsync(request) : await items.UpdateAsync(Guid.Parse(Id), request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash(IsNew ? "Items.Created" : "Common.Saved", saved!.Sku);
        return Redirect($"/items/{saved.Id}");
    }

    public async Task<IActionResult> OnPostAddConversionAsync()
    {
        var id = Guid.Parse(Id);
        if (!await TryExecuteAsync(() => items.AddConversionAsync(id, new UnitConversionRequest(Conversion.FromUnitId, Conversion.ToUnitId, Conversion.Factor)), "Conversion"))
        {
            await OnGetAsync(null);
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/items/{id}/edit#conversions");
    }

    public async Task<IActionResult> OnPostDeleteConversionAsync(Guid conversionId)
    {
        var id = Guid.Parse(Id);
        await items.DeleteConversionAsync(id, conversionId);
        Flash("Common.Deleted");
        return Redirect($"/items/{id}/edit#conversions");
    }

    private async Task LoadAsync()
    {
        Categories = await items.ListCategoriesAsync();
        Units = await reference.ListUnitsAsync();
        TaxCodes = await reference.ListTaxCodesAsync(activeOnly: true);
        Workflows = await workflows.OptionsAsync();
        if (!IsNew)
        {
            Conversions = await items.ListConversionsAsync(Guid.Parse(Id));
        }
    }
}
