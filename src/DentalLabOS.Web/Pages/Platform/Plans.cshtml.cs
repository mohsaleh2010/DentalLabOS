using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Platform;

[Screen("PLT-06")]
[HasPermission(Permissions.Platform.TenantsRead)]
public sealed class PlansModel(PlatformTenantService tenants) : AppPageModel
{
    public IReadOnlyList<PlanDto> Plans { get; private set; } = [];

    public async Task OnGetAsync() => Plans = await tenants.ListPlansAsync(activeOnly: false);
}
