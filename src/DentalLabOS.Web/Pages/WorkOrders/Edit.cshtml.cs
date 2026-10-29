using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-06")]
[HasPermission(Permissions.WorkOrders.Update)]
public sealed class EditModel(WorkOrderService workOrders) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public InputModel Input { get; set; } = new();

    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<OptionDto> Dentists { get; private set; } = [];
    public bool ProductionStarted => Wo.Stages.Any(s => s.StartedAtUtc is not null || s.Status == "Skipped");

    public sealed class InputModel
    {
        public Guid? DentistId { get; set; }
        public string? PatientReference { get; set; }
        public string? MaterialName { get; set; }
        public string? Shade { get; set; }
        public List<string> Teeth { get; set; } = [];
        public int Quantity { get; set; } = 1;
        public DateOnly DueDate { get; set; }
        public string Priority { get; set; } = "Normal";
        public string? PriorityReason { get; set; }
        public string? Instructions { get; set; }
        public string? ChangeSummary { get; set; }
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var w = Wo;
        Input = new InputModel
        {
            DentistId = w.DentistId, PatientReference = w.PatientReference, MaterialName = w.MaterialName, Shade = w.Shade,
            Teeth = w.Teeth.Select(t => t.Code).ToList(), Quantity = w.Quantity, DueDate = w.DueDate, Priority = w.Priority,
            PriorityReason = w.PriorityReason, Instructions = w.Instructions, RowVersion = w.RowVersion,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        var request = new WorkOrderUpdateRequest(i.DentistId, i.PatientReference, i.MaterialName, i.Shade, i.Instructions,
            i.Teeth.Select(t => new ToothRequest(t)).ToList(), i.Quantity, i.DueDate, i.Priority, i.PriorityReason, i.ChangeSummary, i.RowVersion);
        if (!await TryExecuteAsync(() => workOrders.UpdateAsync(Id, request)))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/workorders/{Id}");
    }

    private async Task LoadAsync()
    {
        Wo = await workOrders.GetAsync(Id);
        Dentists = await workOrders.DentistOptionsAsync(Wo.CustomerId);
    }
}
