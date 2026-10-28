using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Production;

[Screen("ORD-13")]
[HasPermission(Permissions.Production.Assign)]
public sealed class AssignModel(ProductionService production, ProductionSetupService setup, WorkOrderService workOrders) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Dept { get; set; }
    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    [BindProperty] public List<Guid> StageIds { get; set; } = [];
    [BindProperty] public Guid? UserId { get; set; }

    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];
    public IReadOnlyList<TechnicianOptionDto> Technicians { get; private set; } = [];
    public IReadOnlyList<TaskDto> Tasks { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (StageIds.Count == 0)
        {
            FlashError("Assign.NothingSelected");
            return Redirect(SelfUrl());
        }

        AssignResultDto? result = null;
        if (!await TryExecuteAsync(async () => result = await workOrders.AssignAsync(new AssignRequest(StageIds, UserId)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash(result!.Skipped > 0 ? "Assign.DoneSkipped" : "Assign.Done", result.Assigned, result.Skipped);
        return Redirect(SelfUrl());
    }

    private string SelfUrl() => $"/production/assign?dept={Dept}&all={All}";

    private async Task LoadAsync()
    {
        Departments = await production.DepartmentOptionsAsync();
        Technicians = await setup.TechnicianOptionsAsync(Dept);
        Tasks = await production.AssignableAsync(Dept, All);
    }
}
