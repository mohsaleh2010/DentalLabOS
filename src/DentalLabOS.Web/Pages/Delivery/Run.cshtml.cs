using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

/// <summary>ORD-23: one courier's run — stops in visiting order, then start (notes go out) and complete (every stop has an outcome).</summary>
[Screen("ORD-23")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class RunModel(DeliveryService delivery) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public DateOnly RunDate { get; set; }
    [BindProperty] public Guid? CourierUserId { get; set; }
    [BindProperty] public string? CourierName { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public string? RowVersion { get; set; }

    public DeliveryRunDto Run { get; private set; } = default!;
    public IReadOnlyList<DeliveryNoteListItemDto> Unplanned { get; private set; } = [];
    public IReadOnlyList<OptionDto> Couriers { get; private set; } = [];

    private string Here => $"/delivery/runs/{Id}";

    public async Task OnGetAsync()
    {
        await LoadAsync();
        RunDate = Run.RunDate;
        CourierUserId = Run.CourierUserId;
        CourierName = Run.CourierUserId is null ? Run.Courier : null;
        Notes = Run.Notes;
        RowVersion = Run.RowVersion;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Can(Permissions.Delivery.Manage))
        {
            return Forbid();
        }

        if (!await TryExecuteAsync(() => delivery.SaveRunAsync(Id, new DeliveryRunRequest(RunDate, CourierUserId, CourierName, Notes, RowVersion)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect(Here);
    }

    public Task<IActionResult> OnPostStopsAsync(List<Guid> noteIds) =>
        Can(Permissions.Delivery.Manage) ? ActAsync(() => delivery.SetStopsAsync(Id, new RunStopsRequest(noteIds)), "Delivery.StopsSaved", Here) : Task.FromResult<IActionResult>(Forbid());

    public Task<IActionResult> OnPostStartAsync() =>
        Can(Permissions.Delivery.Manage) ? ActAsync(() => delivery.StartRunAsync(Id), "Delivery.RunStarted", Here) : Task.FromResult<IActionResult>(Forbid());

    public Task<IActionResult> OnPostCompleteAsync() =>
        Can(Permissions.Delivery.Manage) ? ActAsync(() => delivery.CompleteRunAsync(Id), "Delivery.RunCompleted", Here) : Task.FromResult<IActionResult>(Forbid());

    private async Task LoadAsync()
    {
        Run = await delivery.GetRunAsync(Id);
        if (Can(Permissions.Delivery.Manage))
        {
            Couriers = await delivery.CourierOptionsAsync();
            if (Run.Status == "Planned")
            {
                Unplanned = await delivery.UnplannedNotesAsync();
            }
        }
    }
}
