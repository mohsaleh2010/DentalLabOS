using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.PriceLists;

/// <summary>ITM-09: percentage change on a list, always previewed before it is applied.</summary>
[Screen("ITM-09")]
[HasPermission(Permissions.PriceLists.Bulk)]
public sealed class BulkModel(PriceListService priceLists, ItemService items) : AppPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<PriceListDto> Lists { get; private set; } = [];
    public IReadOnlyList<ItemCategoryDto> Categories { get; private set; } = [];
    public BulkPricePreviewDto? Preview { get; private set; }

    public sealed class InputModel
    {
        public Guid PriceListId { get; set; }
        public decimal Percent { get; set; }
        public Guid? CategoryId { get; set; }
        public string? ItemType { get; set; }
        public int Decimals { get; set; } = 2;
    }

    public async Task OnGetAsync(Guid? list)
    {
        await LoadAsync();
        Input.PriceListId = list ?? Lists.FirstOrDefault(l => l.IsDefault)?.Id ?? Guid.Empty;
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        await TryExecuteAsync(async () => Preview = await priceLists.PreviewBulkAsync(BuildRequest()));
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApplyAsync()
    {
        var updated = 0;
        if (!await TryExecuteAsync(async () => updated = await priceLists.ApplyBulkAsync(BuildRequest())))
        {
            await LoadAsync();
            return Page();
        }

        Flash("PriceLists.BulkApplied", updated);
        return Redirect($"/price-lists/{Input.PriceListId}");
    }

    private BulkPriceUpdateRequest BuildRequest() =>
        new(Input.PriceListId, Input.Percent, Input.CategoryId, string.IsNullOrEmpty(Input.ItemType) ? null : Input.ItemType, Input.Decimals);

    private async Task LoadAsync()
    {
        Lists = await priceLists.ListAsync();
        Categories = Can(Permissions.Items.Read) ? await items.ListCategoriesAsync() : [];
    }
}
