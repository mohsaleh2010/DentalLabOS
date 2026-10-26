using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.PriceLists;

[Screen("ITM-07")]
[HasPermission(Permissions.PriceLists.Read)]
public sealed class IndexModel(PriceListService priceLists) : AppPageModel
{
    public IReadOnlyList<PriceListDto> Lists { get; private set; } = [];

    public async Task OnGetAsync() => Lists = await priceLists.ListAsync();
}
