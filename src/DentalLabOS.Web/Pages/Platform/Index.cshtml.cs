using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Platform;

[Screen("PLT-01")]
[HasPermission(Permissions.Platform.DashboardRead)]
public sealed class IndexModel(PlatformTenantService tenants) : AppPageModel
{
    public PlatformDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await tenants.GetDashboardAsync();
}
