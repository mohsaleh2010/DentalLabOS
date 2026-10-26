using DentalLabOS.Application.Organization;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Departments;

[Screen("ADM-04")]
[HasPermission(Permissions.Admin.DepartmentsManage)]
public sealed class EditModel(DepartmentService departments, BranchService branches) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "new";

    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<BranchDto> Branches { get; private set; } = [];

    public bool IsNew => !Guid.TryParse(Id, out _);

    public sealed class InputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Guid? BranchId { get; set; }
        public bool IsProduction { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadBranchesAsync();
        if (!IsNew)
        {
            var d = await departments.GetAsync(Guid.Parse(Id));
            Input = new InputModel { Code = d.Code, Name = d.Name, BranchId = d.BranchId, IsProduction = d.IsProduction, IsActive = d.IsActive, RowVersion = d.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new DepartmentUpsertRequest(Input.Code, Input.Name, Input.BranchId, Input.IsProduction, IsNew || Input.IsActive, Input.RowVersion);
        if (!await TryExecuteAsync(async () =>
            {
                if (IsNew)
                {
                    await departments.CreateAsync(request);
                }
                else
                {
                    await departments.UpdateAsync(Guid.Parse(Id), request);
                }
            }))
        {
            await LoadBranchesAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/departments");
    }

    private async Task LoadBranchesAsync() => Branches = await branches.ListOptionsAsync();
}
