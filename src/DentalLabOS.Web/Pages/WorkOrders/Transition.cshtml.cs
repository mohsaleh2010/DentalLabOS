using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

/// <summary>ORD-15: move a work order along its route. Every option is re-validated by the workflow engine on the server.</summary>
[Screen("ORD-15")]
[HasPermission(Permissions.WorkOrders.Transition)]
public sealed class TransitionModel(WorkOrderService workOrders) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    /// <summary>The chosen option as <c>action|stageId</c> (start, complete, skip, back).</summary>
    [BindProperty] public string? Pick { get; set; }
    [BindProperty] public string? Reason { get; set; }

    public string Action { get; private set; } = string.Empty;
    public Guid StageId { get; private set; }
    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<WorkOrderStageDto> Available => Wo.Stages.Where(s => s.IsAvailable && s.Status != "AwaitingQuality").ToList();
    public IReadOnlyList<WorkOrderStageDto> Earlier { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var parts = (Pick ?? string.Empty).Split('|');
        Action = parts[0];
        StageId = parts.Length == 2 && Guid.TryParse(parts[1], out var stage) ? stage : Guid.Empty;
        if (StageId == Guid.Empty || Action is not ("start" or "complete" or "skip" or "back"))
        {
            ModelState.AddModelError(string.Empty, L["WorkOrders.ChooseAction"]);
            await LoadAsync();
            return Page();
        }

        var ok = await TryExecuteAsync(() => Action switch
        {
            "start" => workOrders.StartStageAsync(Id, StageId),
            "skip" => workOrders.SkipStageAsync(Id, StageId, Reason),
            "back" => workOrders.MoveBackAsync(Id, StageId, Reason),
            _ => workOrders.CompleteStageAsync(Id, StageId),
        }, string.Empty);
        if (!ok)
        {
            await LoadAsync();
            return Page();
        }

        Flash(Action switch { "start" => "WorkOrders.StageStarted", "skip" => "WorkOrders.StageSkipped", "back" => "WorkOrders.MovedBack", _ => "WorkOrders.StageCompleted" });
        return Redirect($"/workorders/{Id}?tab=stages");
    }

    private async Task LoadAsync()
    {
        Wo = await workOrders.GetAsync(Id);
        var current = Wo.Stages.Where(s => s.Status is not ("Done" or "Skipped")).Select(s => (int?)s.Step).Min() ?? int.MaxValue;
        Earlier = Wo.Stages.Where(s => s.Step < current).GroupBy(s => s.Step).Select(g => g.First()).ToList();
    }
}
