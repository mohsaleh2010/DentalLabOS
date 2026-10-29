using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Users;

[Screen("ADM-07")]
[HasPermission(Permissions.Admin.TechniciansManage)]
public sealed class TechnicianModel(ProductionSetupService setup, ProductionService production) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public InputModel Input { get; set; } = new();

    public TechnicianProfileDto Profile { get; private set; } = default!;
    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];
    public bool CanSeeRate => Can(Permissions.Admin.CostingManage);

    public sealed class InputModel
    {
        public Guid DepartmentId { get; set; }
        public decimal? LaborRatePerHour { get; set; }
        public string? Skills { get; set; }
        public decimal DifficultyCapacityPerDay { get; set; }
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var p = Profile;
        Input = new InputModel
        {
            DepartmentId = p.DepartmentId ?? Guid.Empty, LaborRatePerHour = p.LaborRatePerHour, Skills = p.Skills,
            DifficultyCapacityPerDay = p.DifficultyCapacityPerDay, RowVersion = p.RowVersion,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var request = new TechnicianProfileRequest(i.DepartmentId, CanSeeRate ? i.LaborRatePerHour : null, i.Skills, i.DifficultyCapacityPerDay, i.RowVersion);
        if (!await TryExecuteAsync(() => setup.SaveTechnicianAsync(Id, request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/admin/users/{Id}/technician");
    }

    private async Task LoadAsync()
    {
        Profile = await setup.GetTechnicianAsync(Id);
        Departments = await production.DepartmentOptionsAsync();
    }
}
