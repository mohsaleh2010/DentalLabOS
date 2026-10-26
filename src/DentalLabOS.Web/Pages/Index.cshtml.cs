using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DentalLabOS.Web.Pages;

/// <summary>Routes each user to their home: platform portal, lab dashboard or profile.</summary>
public sealed class IndexModel(ICurrentUser currentUser, IPermissionService permissions) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if (!currentUser.IsAuthenticated)
        {
            return Redirect("/login");
        }

        if (currentUser.TenantId is null && currentUser.IsPlatformStaff)
        {
            return Redirect("/platform");
        }

        return Redirect(await permissions.HasAsync(Permissions.Admin.DashboardRead) ? "/admin" : "/account");
    }
}
