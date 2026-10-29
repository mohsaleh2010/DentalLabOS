using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

/// <summary>ORD-21: orders that passed quality, grouped by clinic; the chosen ones go on one delivery note.</summary>
[Screen("ORD-21")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class ReadyModel(DeliveryService delivery) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public IReadOnlyList<ReadyGroupDto> Groups { get; private set; } = [];
    public IReadOnlyList<DeliveryRunListItemDto> Runs { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync(Guid customerId, List<Guid> workOrderIds, Guid clientOperationId, Guid? runId)
    {
        DeliveryNoteDto? note = null;
        if (!await TryExecuteAsync(async () => note = await delivery.CreateNoteAsync(
                new DeliveryNoteCreateRequest(customerId, workOrderIds, clientOperationId, null, runId)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Delivery.NoteCreated", note!.Number);
        return Redirect($"/delivery/notes/{note.Id}");
    }

    private async Task LoadAsync()
    {
        Groups = await delivery.ReadyAsync(Search);
        if (Can(Permissions.Delivery.Manage))
        {
            Runs = (await delivery.ListRunsAsync()).Where(r => r.Status == "Planned").ToList();
        }
    }
}
