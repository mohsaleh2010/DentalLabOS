using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

/// <summary>ORD-24: who received the note, or why it could not be delivered. A failed note puts its orders back on the ready list.</summary>
[Screen("ORD-24")]
[HasPermission(Permissions.Delivery.Confirm)]
[RequestSizeLimit(FileInspector.MaxBytes + 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = FileInspector.MaxBytes + 1024 * 1024)]
public sealed class ConfirmModel(DeliveryService delivery, ProductionSetupService setup, LabCalendar calendar) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public string Outcome { get; set; } = "Delivered";
    [BindProperty] public string? ReceivedByName { get; set; }
    [BindProperty] public DateTime? DeliveredAt { get; set; }
    [BindProperty] public Guid? FailureReasonCodeId { get; set; }
    [BindProperty] public string? FailureNotes { get; set; }
    [BindProperty] public string? RowVersion { get; set; }

    public DeliveryNoteDto Note { get; private set; } = default!;
    public IReadOnlyList<ReasonCodeDto> Reasons { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
        RowVersion = Note.RowVersion;
        DeliveredAt = await calendar.NowLocalAsync();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? proof)
    {
        var delivered = Outcome != "Failed";
        DateTime? atUtc = null;
        if (delivered && DeliveredAt is { } local)
        {
            atUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), await calendar.ZoneAsync());
        }

        var ok = await TryExecuteAsync(async () =>
        {
            // The proof goes first so a rejected file leaves the note open for another try.
            if (delivered && proof is { Length: > 0 })
            {
                await using var stream = proof.OpenReadStream();
                await delivery.UploadProofAsync(Id, stream, proof.FileName);
            }

            await delivery.ConfirmAsync(Id, new DeliveryConfirmRequest(delivered, ReceivedByName, atUtc, delivered ? null : FailureReasonCodeId,
                delivered ? null : FailureNotes, delivered && proof is { Length: > 0 } ? null : RowVersion));
        }, string.Empty);
        if (!ok)
        {
            await LoadAsync();
            return Page();
        }

        Flash(delivered ? "Delivery.ConfirmedDelivered" : "Delivery.ConfirmedFailed");
        return Redirect(Note?.RunId is { } run ? $"/delivery/runs/{run}" : $"/delivery/notes/{Id}");
    }

    private async Task LoadAsync()
    {
        Note = await delivery.GetNoteAsync(Id);
        Reasons = await setup.ReasonOptionsAsync(ReasonScope.DeliveryFailure);
    }
}
