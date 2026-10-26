using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Users;

[Screen("ADM-05")]
[HasPermission(Permissions.Admin.UsersRead)]
public sealed class IndexModel(UserAdminService users) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<UserSummaryDto> Result { get; private set; } = default!;
    public IReadOnlyList<InvitationDto> Invitations { get; private set; } = [];
    public bool CanManage { get; private set; }

    public string PageUrl => $"/admin/users?q={Uri.EscapeDataString(Q ?? string.Empty)}&page={{page}}";

    public async Task OnGetAsync()
    {
        Result = await users.ListAsync(new PageQuery(PageNumber, 20, Q));
        Invitations = await users.ListInvitationsAsync();
        CanManage = await PermissionService.HasAsync(Permissions.Admin.UsersManage);
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid invitationId)
    {
        await users.RevokeInvitationAsync(invitationId);
        Flash("Admin.Users.Revoked");
        return RedirectToPage();
    }
}
