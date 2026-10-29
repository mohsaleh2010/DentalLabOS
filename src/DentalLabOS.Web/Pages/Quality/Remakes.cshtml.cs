using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Quality;

[Screen("ORD-20")]
[HasPermission(Permissions.Quality.Read)]
public sealed class RemakesModel(QualityService quality) : AppPageModel
{
    public IReadOnlyList<RemakeListItemDto> Items { get; private set; } = [];

    public async Task OnGetAsync() => Items = await quality.RemakesAsync();
}
