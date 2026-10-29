using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

/// <summary>ORD-22: a delivery note. Every line's packaging is checked before it leaves; the server re-checks each step.</summary>
[Screen("ORD-22")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class NoteModel(DeliveryService delivery) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public DeliveryNoteDto Note { get; private set; } = default!;

    private string Here => $"/delivery/notes/{Id}";

    public async Task OnGetAsync() => Note = await delivery.GetNoteAsync(Id);

    public Task<IActionResult> OnPostPackagingAsync(List<Guid> lineIds) =>
        Demand(Permissions.Delivery.Manage) ?? ActAsync(async () =>
        {
            var note = await delivery.GetNoteAsync(Id);
            var all = note.Lines.Select(l => l.Id).ToList();
            await delivery.CheckPackagingAsync(Id, new PackagingCheckRequest(lineIds));
            var open = all.Except(lineIds).ToList();
            if (open.Count > 0)
            {
                await delivery.CheckPackagingAsync(Id, new PackagingCheckRequest(open, false));
            }
        }, "Delivery.PackagingSaved", Here);

    public Task<IActionResult> OnPostRemoveLineAsync(Guid lineId) =>
        Demand(Permissions.Delivery.Manage) ?? ActAsync(() => delivery.RemoveLineAsync(Id, lineId), "Delivery.LineRemoved", Here);

    public Task<IActionResult> OnPostDispatchAsync() =>
        Demand(Permissions.Delivery.Manage) ?? ActAsync(() => delivery.DispatchAsync(Id), "Delivery.Dispatched", Here);

    public Task<IActionResult> OnPostCancelAsync() =>
        Demand(Permissions.Delivery.Manage) ?? ActAsync(() => delivery.CancelNoteAsync(Id), "Delivery.Cancelled", Here);

    public async Task<IActionResult> OnGetProofAsync()
    {
        var file = await delivery.DownloadProofAsync(Id);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Shown actions follow the user's permissions; the service still refuses anyone without them.</summary>
    private Task<IActionResult>? Demand(string permission) => Can(permission) ? null : Task.FromResult<IActionResult>(Forbid());
}
