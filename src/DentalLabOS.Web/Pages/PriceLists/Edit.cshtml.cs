using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.PriceLists;

/// <summary>ITM-08: list header and its lines. Read-only for users without <c>pricelists.manage</c>.</summary>
[Screen("ITM-08")]
[HasPermission(Permissions.PriceLists.Read)]
public sealed class EditModel(PriceListService priceLists, ItemService items, ReferenceDataService reference) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public LineInput Line { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public bool CanManage => Can(Permissions.PriceLists.Manage);
    public PriceListDto? List { get; private set; }
    public PagedResult<PriceListLineDto>? Lines { get; private set; }
    public IReadOnlyList<CurrencyDto> Currencies { get; private set; } = [];
    public IReadOnlyList<OptionDto> ItemOptions { get; private set; } = [];

    public string PageUrl => $"/price-lists/{Id}?q={Uri.EscapeDataString(Q ?? string.Empty)}&page={{page}}";

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public sealed class LineInput
    {
        public Guid ItemId { get; set; }
        public decimal Price { get; set; }
        public decimal MinQty { get; set; } = 1;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew && !CanManage)
        {
            return Forbid();
        }

        await LoadAsync(fillInput: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new PriceListUpsertRequest(Input.Name, Input.CurrencyCode, Input.IsDefault, Input.ValidFrom, Input.ValidTo, Input.IsActive, Input.RowVersion);
        PriceListDto? saved = null;
        if (!await TryExecuteAsync(async () => saved = await priceLists.SaveAsync(IsNew ? null : Guid.Parse(Id), request)))
        {
            await LoadAsync(fillInput: false);
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/price-lists/{saved!.Id}");
    }

    public async Task<IActionResult> OnPostLineAsync()
    {
        var id = Guid.Parse(Id);
        if (!await TryExecuteAsync(() => priceLists.SetLineAsync(id, new PriceListLineRequest(Line.ItemId, Line.Price, Line.MinQty)), "Line"))
        {
            await LoadAsync(fillInput: true);
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/price-lists/{id}#lines");
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid lineId)
    {
        var id = Guid.Parse(Id);
        await priceLists.DeleteLineAsync(id, lineId);
        Flash("Common.Deleted");
        return Redirect($"/price-lists/{id}?q={Uri.EscapeDataString(Q ?? string.Empty)}#lines");
    }

    private async Task LoadAsync(bool fillInput)
    {
        Currencies = await reference.ListCurrenciesAsync();
        if (!IsNew)
        {
            var id = Guid.Parse(Id);
            List = await priceLists.GetAsync(id);
            Lines = await priceLists.ListLinesAsync(id, new Contracts.PageQuery(PageNumber, 50, Q));
            if (fillInput)
            {
                Input = new InputModel
                {
                    Name = List.Name, CurrencyCode = List.CurrencyCode, IsDefault = List.IsDefault, ValidFrom = List.ValidFrom, ValidTo = List.ValidTo,
                    IsActive = List.IsActive, RowVersion = List.RowVersion,
                };
            }

            if (CanManage && Can(Permissions.Items.Read))
            {
                ItemOptions = await items.OptionsAsync(null);
            }
        }
        else if (fillInput)
        {
            Input.CurrencyCode = Currencies.FirstOrDefault(c => c.IsBase)?.Code ?? string.Empty;
        }
    }
}
