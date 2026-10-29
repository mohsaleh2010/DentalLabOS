using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

/// <summary>ORD-19: the clinic returned a delivered case for correction; a linked adjustment order is opened.</summary>
[Screen("ORD-19")]
[HasPermission(Permissions.Quality.RemakeManage)]
public sealed class AdjustmentModel(QualityService quality, WorkOrderService workOrders, ProductionSetupService setup, LabCalendar calendar) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid ReasonCodeId { get; set; }
    [BindProperty] public string? Notes { get; set; }
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
        if (!await TryExecuteAsync(async () => created = await quality.RequestCustomerAdjustmentAsync(Id,
                new CustomerAdjustmentRequest(ReasonCodeId, ClientOperationId, Notes, DueDate, Priority, PriorityReason)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Adjustments.Created", created!.Number);
        return Redirect($"/workorders/{created.WorkOrderId}");
    }

    private async Task LoadAsync()
    {
        Wo = await workOrders.GetAsync(Id);
        Reasons = await setup.ReasonOptionsAsync(ReasonScope.Adjustment);
        Today = await calendar.TodayAsync();
    }
}
