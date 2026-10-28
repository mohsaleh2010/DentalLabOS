using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.WorkOrders;

/// <summary>ORD-26: printable job ticket that travels with the case. The QR holds only the work-order number (no patient data).</summary>
[Screen("ORD-26")]
[HasPermission(Permissions.WorkOrders.Print)]
public sealed class TicketModel(WorkOrderService workOrders, IQrCodeRenderer qr, LayoutContext layout) : AppPageModel
{
    public WorkOrderDto Wo { get; private set; } = default!;
    public string QrSvg { get; private set; } = string.Empty;
    public string? LabName { get; private set; }

    public async Task OnGetAsync(Guid id)
    {
        Wo = await workOrders.GetAsync(id);
        QrSvg = qr.Svg(Wo.Number);
        LabName = (await layout.GetAsync()).LabName;
    }
}
