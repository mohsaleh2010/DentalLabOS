using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Quality;

/// <summary>ORD-20: open a remake linked to the original. Who bears the cost is a recorded human decision.</summary>
[Screen("ORD-20")]
[HasPermission(Permissions.Quality.RemakeManage)]
public sealed class NewRemakeModel(QualityService quality, WorkOrderService workOrders, ProductionSetupService setup, LabCalendar calendar) : AppPageModel
{
    public static readonly string[] ChargeOptions = ["Lab", "Customer", "Supplier", "Undecided"];

    [BindProperty(SupportsGet = true)] public Guid WorkOrderId { get; set; }
    [BindProperty] public Guid ReasonCodeId { get; set; }
    [BindProperty] public string ChargeTo { get; set; } = "Lab";
    [BindProperty] public string? Decision { get; set; }
    [BindProperty] public DateOnly? DueDate { get; set; }
    [BindProperty] public string Priority { get; set; } = "High";
    [BindProperty] public string? PriorityReason { get; set; }
    [BindProperty] public Guid ClientOperationId { get; set; } = Guid.NewGuid();

    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<ReasonCodeDto> Reasons { get; private set; } = [];
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        LinkedWorkOrderResultDto? created = null;
        if (!await TryExecuteAsync(async () => created = await quality.CreateRemakeAsync(
                new RemakeRequest(WorkOrderId, ReasonCodeId, ChargeTo, ClientOperationId, Decision, DueDate, Priority, PriorityReason)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Remakes.Created", created!.Number);
        return Redirect($"/workorders/{created.WorkOrderId}");
    }

    private async Task LoadAsync()
    {
        Wo = await workOrders.GetAsync(WorkOrderId);
        Reasons = await setup.ReasonOptionsAsync(ReasonScope.Remake);
        Today = await calendar.TodayAsync();
    }
}
