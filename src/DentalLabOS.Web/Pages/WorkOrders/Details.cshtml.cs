using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-07")]
[HasPermission(Permissions.WorkOrders.Read)]
[RequestSizeLimit(IntakeModel.MaxUploadBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = IntakeModel.MaxUploadBytes)]
public sealed class DetailsModel(WorkOrderService workOrders) : AppPageModel
{
    public static readonly string[] Tabs = ["overview", "stages", "files", "notes", "timeline"];

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "overview";

    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<TimelineEntryDto> Timeline { get; private set; } = [];

    private string Here(string tab) => $"/workorders/{Id}" + (tab == "overview" ? string.Empty : $"?tab={tab}");

    public async Task OnGetAsync()
    {
        Tab = Tabs.Contains(Tab) ? Tab : "overview";
        Wo = await workOrders.GetAsync(Id);
        if (Tab is "overview" or "timeline")
        {
            Timeline = await workOrders.TimelineAsync(Id);
        }
    }

    public Task<IActionResult> OnPostStartAsync(Guid stageId, string? back) =>
        ActAsync(() => workOrders.StartStageAsync(Id, stageId), "WorkOrders.StageStarted", LocalOr(back, Here("stages")));

    public Task<IActionResult> OnPostPauseAsync(Guid stageId, string? back) =>
        ActAsync(() => workOrders.PauseStageAsync(Id, stageId, null), "WorkOrders.StagePaused", LocalOr(back, Here("stages")));

    public Task<IActionResult> OnPostCompleteAsync(Guid stageId, string? back) =>
        ActAsync(() => workOrders.CompleteStageAsync(Id, stageId), "WorkOrders.StageCompleted", LocalOr(back, Here("stages")));

    public Task<IActionResult> OnPostReleaseAsync() =>
        ActAsync(() => workOrders.ReleaseAsync(Id), "WorkOrders.Released", Here("overview"));

    public Task<IActionResult> OnPostNoteAsync(string? text, bool isInternal = true) =>
        ActAsync(() => workOrders.AddNoteAsync(Id, new NoteRequest(text ?? string.Empty, isInternal)), "WorkOrders.NoteAdded", Here("notes"));

    public async Task<IActionResult> OnPostUploadAsync(List<IFormFile> files)
    {
        var failed = new List<string>();
        var done = 0;
        foreach (var file in files.Where(f => f.Length > 0))
        {
            try
            {
                await using var stream = file.OpenReadStream();
                await workOrders.UploadAsync(Id, stream, file.FileName);
                done++;
            }
            catch (Exception ex) when (ex is DentalLabOS.Application.Common.ValidationException or DentalLabOS.Domain.Common.DomainException)
            {
                failed.Add($"{DentalLabOS.Application.Common.FileInspector.SafeName(file.FileName)}: {ErrorText(ex)}");
            }
        }

        if (done > 0)
        {
            Flash("WorkOrders.FilesUploaded", done);
        }

        if (failed.Count > 0)
        {
            TempData["toast-error"] = string.Join(" · ", failed);
        }

        return Redirect(Here("files"));
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid fileId)
    {
        var file = await workOrders.DownloadAsync(Id, fileId);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public Task<IActionResult> OnPostDeleteFileAsync(Guid fileId) =>
        ActAsync(() => workOrders.DeleteAttachmentAsync(Id, fileId), "Common.Deleted", Here("files"));

    private string ErrorText(Exception ex) => ex switch
    {
        DentalLabOS.Application.Common.ValidationException v => L[v.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Errors.Validation"].Value,
        DentalLabOS.Domain.Common.DomainException d => L[d.MessageKey].Value,
        _ => ex.Message,
    };
}
