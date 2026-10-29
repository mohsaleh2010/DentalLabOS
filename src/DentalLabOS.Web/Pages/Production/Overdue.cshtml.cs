using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Production;

[Screen("ORD-16")]
[HasPermission(Permissions.Production.Read)]
public sealed class OverdueModel(ProductionService production) : AppPageModel
{
    public OverdueDto Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await production.OverdueAsync();
}
