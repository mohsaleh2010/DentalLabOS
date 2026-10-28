using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-28")]
[HasPermission(Permissions.WorkOrders.Cancel)]
public sealed class CancelModel(WorkOrderService workOrders, ProductionSetupService setup) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid ReasonCodeId { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public WorkOrderDto Wo { get; private set; } = default!;
    public IReadOnlyList<ReasonCodeDto> Reasons { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await TryExecuteAsync(() => workOrders.CancelAsync(Id, new CancelRequest(ReasonCodeId, Notes)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("WorkOrders.Cancelled");
        return Redirect($"/workorders/{Id}");
    }

    private async Task LoadAsync()
    {
        Wo = await workOrders.GetAsync(Id);
        Reasons = await setup.ReasonOptionsAsync(ReasonScope.Cancellation);
    }
}
