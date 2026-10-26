using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Users;

[Screen("ADM-06")]
[HasPermission(Permissions.Admin.UsersRead)]
public sealed class DetailsModel(UserAdminService users, RoleService roles) : AppPageModel
{
    public UserSummaryDto UserInfo { get; private set; } = default!;
    public IReadOnlyList<Guid> RoleIds { get; private set; } = [];
    public IReadOnlyList<RoleDto> Roles { get; private set; } = [];
    public bool CanManage { get; private set; }

    public async Task OnGetAsync(Guid id) => await LoadAsync(id);

    public async Task<IActionResult> OnPostRolesAsync(Guid id, List<Guid> roleIds)
    {
        if (await TryExecuteAsync(() => users.AssignRolesAsync(id, roleIds), prefix: string.Empty))
        {
            Flash("Common.Saved");
            return RedirectToPage(new { id });
        }

        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostActiveAsync(Guid id, bool active)
    {
        if (await TryExecuteAsync(() => users.SetActiveAsync(id, active), prefix: string.Empty))
        {
            Flash(active ? "Admin.Users.Activated" : "Admin.Users.Deactivated");
            return RedirectToPage(new { id });
        }

        await LoadAsync(id);
        return Page();
    }

    private async Task LoadAsync(Guid id)
    {
        (UserInfo, RoleIds) = await users.GetAsync(id);
        CanManage = await PermissionService.HasAsync(Permissions.Admin.UsersManage);
        Roles = CanManage ? await roles.ListAssignableAsync() : [];
    }
}
