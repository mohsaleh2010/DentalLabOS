using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Roles;

[Screen("ADM-09")]
[HasPermission(Permissions.Admin.RolesManage)]
public sealed class PermissionsModel(RoleService roles) : AppPageModel
{
    public RolePermissionsDto Matrix { get; private set; } = default!;

    public async Task OnGetAsync(Guid id) => Matrix = await roles.GetPermissionsAsync(id);

    public async Task<IActionResult> OnPostAsync(Guid id, List<string> keys, string rowVersion)
    {
        if (await TryExecuteAsync(() => roles.UpdatePermissionsAsync(id, new UpdateRolePermissionsRequest(keys, rowVersion)), prefix: string.Empty))
        {
            Flash("Admin.Roles.Saved");
            return RedirectToPage(new { id });
        }

        Matrix = await roles.GetPermissionsAsync(id);
        return Page();
    }
}
