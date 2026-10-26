using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Admin;

[Screen("ADM-26")]
[HasPermission(Permissions.Admin.SubscriptionRead)]
public sealed class SubscriptionModel(LabDashboardService dashboard) : AppPageModel
{
    public LabDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await dashboard.GetAsync();
}
