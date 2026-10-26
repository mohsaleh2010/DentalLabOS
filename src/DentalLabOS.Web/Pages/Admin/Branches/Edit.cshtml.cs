using DentalLabOS.Application.Organization;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Branches;

[Screen("ADM-03")]
[HasPermission(Permissions.Admin.BranchesManage)]
public sealed class EditModel(BranchService branches) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";

    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => !Guid.TryParse(Id, out _);
    public bool IsMain { get; private set; }

    public sealed class InputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsNew)
        {
            var b = await branches.GetAsync(Guid.Parse(Id));
            IsMain = b.IsMain;
            Input = new InputModel { Code = b.Code, Name = b.Name, Phone = b.Phone, Address = b.Address, IsActive = b.IsActive, RowVersion = b.RowVersion };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsNew)
        {
            IsMain = (await branches.GetAsync(Guid.Parse(Id))).IsMain;
        }

        var request = new BranchUpsertRequest(Input.Code, Input.Name, Input.Address, Input.Phone, IsMain || Input.IsActive, Input.RowVersion);
        var ok = await TryExecuteAsync(() => IsNew ? branches.CreateAsync(request) : branches.UpdateAsync(Guid.Parse(Id), request));
        if (!ok)
        {
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/branches");
    }
}
