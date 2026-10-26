using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Admin;

[Screen("ADM-01")]
[HasPermission(Permissions.Admin.DashboardRead)]
public sealed class IndexModel(LabDashboardService dashboard) : AppPageModel
{
    public LabDashboardDto Data { get; private set; } = default!;

    public string FirstName => (User.FindFirst(AppClaimTypes.FullName)?.Value ?? string.Empty).Split(' ')[0];

    public async Task OnGetAsync() => Data = await dashboard.GetAsync();
}
