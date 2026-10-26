using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Users;

[Screen("ADM-06")]
[HasPermission(Permissions.Admin.UsersManage)]
public sealed class InviteModel(UserAdminService users, RoleService roles) : AppPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<RoleDto> Roles { get; private set; } = [];
    public InviteUserResult? Result { get; private set; }

    // E-mail delivery arrives with the notification module; until then the link is shown once to the inviter.
    public string InviteUrl => Result is null ? string.Empty : $"{Request.Scheme}://{Request.Host}/invite/accept?token={Result.AcceptUrlToken}";

    public sealed class InputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
    }

    public async Task OnGetAsync() => Roles = await roles.ListAssignableAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Roles = await roles.ListAssignableAsync();
        await TryExecuteAsync(async () => Result = await users.InviteAsync(new InviteUserRequest(Input.Email, Input.FullName, Input.RoleId)));
        return Page();
    }
}
