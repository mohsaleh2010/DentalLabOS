using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using DentalLabOS.Web.Pages.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Quality;

/// <summary>ORD-18: the inspector answers the stage's checklist and accepts, rejects or asks for an adjustment. The engine re-checks every rule.</summary>
[Screen("ORD-18")]
[HasPermission(Permissions.Quality.Inspect)]
[RequestSizeLimit(IntakeModel.MaxUploadBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = IntakeModel.MaxUploadBytes)]
public sealed class InspectModel(QualityService quality, WorkOrderService workOrders, ProductionSetupService setup) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? StageId { get; set; }
    [BindProperty] public string? Result { get; set; }
    [BindProperty] public List<ItemInput> Items { get; set; } = [];
    [BindProperty] public Guid? RejectReasonId { get; set; }
    [BindProperty] public Guid? AdjustReasonId { get; set; }
    [BindProperty] public Guid? ReturnToStageId { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public QualityInspectionDto Inspection { get; private set; } = default!;
    public IReadOnlyList<ReasonCodeDto> RejectReasons { get; private set; } = [];
    public IReadOnlyList<ReasonCodeDto> AdjustReasons { get; private set; } = [];

    public sealed class ItemInput
    {
        public Guid TemplateItemId { get; set; }
        public bool? Passed { get; set; }
        public string? Note { get; set; }
    }

    public bool? Answer(Guid itemId) => Items.FirstOrDefault(i => i.TemplateItemId == itemId)?.Passed;

    public string? NoteFor(Guid itemId) => Items.FirstOrDefault(i => i.TemplateItemId == itemId)?.Note;

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            FlashError(ex.MessageKey);
            return Redirect($"/workorders/{Id}?tab=quality");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(List<IFormFile> photos)
    {
        var answered = Items.Where(i => i.Passed is not null).Select(i => new QualityCheckItemRequest(i.TemplateItemId, i.Passed!.Value, i.Note)).ToList();
        var reason = Result switch { "Rejected" => RejectReasonId, "AdjustmentRequested" => AdjustReasonId, _ => null };
        QualityCheckResultDto? done = null;
        var ok = await TryExecuteAsync(async () => done = await quality.InspectAsync(Id,
            new QualityCheckRequest(StageId ?? Guid.Empty, Result ?? string.Empty, answered, reason, Result == "Accepted" ? null : ReturnToStageId, Notes)), string.Empty);
        if (!ok)
        {
            return await OnGetAsync();
        }

        // Evidence photos go to the work order's files; a bad file doesn't undo the decision.
        var failed = 0;
        foreach (var photo in photos.Where(p => p.Length > 0))
        {
            try
            {
                await using var stream = photo.OpenReadStream();
                await workOrders.UploadAsync(Id, stream, photo.FileName);
            }
            catch (Exception ex) when (ex is DentalLabOS.Application.Common.ValidationException or DomainException)
            {
                failed++;
            }
        }

        Flash(Result switch { "Accepted" => "Quality.Accepted", "Rejected" => "Quality.Rejected", _ => "Quality.AdjustmentSent" });
        if (failed > 0)
        {
            FlashError("Quality.PhotosFailed");
        }

        return Redirect(done!.WorkOrderStatus == "InQuality" ? "/quality/queue" : $"/workorders/{Id}?tab=quality");
    }

    private async Task LoadAsync()
    {
        Inspection = await quality.InspectionAsync(Id, StageId);
        StageId = Inspection.StageId;
        RejectReasons = await setup.ReasonOptionsAsync(ReasonScope.Rejection);
        AdjustReasons = await setup.ReasonOptionsAsync(ReasonScope.Adjustment);
        ReturnToStageId ??= Inspection.ReturnTargets.LastOrDefault()?.Id;
    }
}
