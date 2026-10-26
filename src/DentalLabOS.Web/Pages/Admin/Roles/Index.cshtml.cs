using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Roles;

[Screen("ADM-08")]
[HasPermission(Permissions.Admin.RolesManage)]
public sealed class IndexModel(RoleService roles) : AppPageModel
{
    public IReadOnlyList<RoleDto> Roles { get; private set; } = [];

    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? CopyFromRoleId { get; set; }
    }

    public async Task OnGetAsync() => Roles = await roles.ListAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Guid id = default;
        if (await TryExecuteAsync(async () => id = await roles.CreateAsync(new CreateRoleRequest(Input.Name, Input.Description, Input.CopyFromRoleId))))
        {
            Flash("Common.Saved");
            return Redirect($"/admin/roles/{id}/permissions");
        }

        Roles = await roles.ListAsync();
        return Page();
    }
}
