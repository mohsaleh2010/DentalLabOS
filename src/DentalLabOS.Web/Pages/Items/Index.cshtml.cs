using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-01")]
[HasPermission(Permissions.Items.Read)]
public sealed class IndexModel(ItemService items) : AppPageModel
{
    public static readonly string[] Types = ["Service", "OutsourcedService", "Material", "Consumable", "FinishedGood"];

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<ItemListItemDto> Result { get; private set; } = default!;
    public IReadOnlyList<ItemCategoryDto> Categories { get; private set; } = [];

    public string PageUrl =>
        $"/items?q={Uri.EscapeDataString(Q ?? string.Empty)}&type={Type}&category={Category}&status={Status}&page={{page}}";

    public async Task OnGetAsync()
    {
        bool? active = Status switch { "active" => true, "inactive" => false, _ => null };
        Result = await items.ListAsync(new PageQuery(PageNumber, 25, Q), new ItemFilter(Type, Category, active));
        Categories = await items.ListCategoriesAsync();
    }
}
