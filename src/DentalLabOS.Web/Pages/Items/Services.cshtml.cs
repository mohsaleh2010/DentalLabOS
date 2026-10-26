using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

/// <summary>ITM-05: the lab's service catalog as cards grouped by category (what reception quotes from).</summary>
[Screen("ITM-05")]
[HasPermission(Permissions.Items.ServicesRead)]
public sealed class ServicesModel(ItemService items) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public IReadOnlyList<IGrouping<string, ItemListItemDto>> Groups { get; private set; } = [];
    public int Count { get; private set; }

    public async Task OnGetAsync()
    {
        var result = await items.ListAsync(new PageQuery(1, PageQuery.MaxPageSize, Q), new ItemFilter(Active: true, ServicesOnly: true));
        Count = result.TotalCount;
        Groups = result.Items.GroupBy(i => i.CategoryName).OrderBy(g => g.Key).ToList();
    }
}
