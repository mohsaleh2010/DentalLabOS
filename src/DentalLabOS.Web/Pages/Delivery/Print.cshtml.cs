using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Delivery;

/// <summary>ORD-22: the printed note the courier hands over and the clinic signs. The QR holds only the note number.</summary>
[Screen("ORD-22")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class PrintModel(DeliveryService delivery, IQrCodeRenderer qr, LayoutContext layout) : AppPageModel
{
    public DeliveryNoteDto Note { get; private set; } = default!;
    public string QrSvg { get; private set; } = string.Empty;
    public string? LabName { get; private set; }

    public async Task OnGetAsync(Guid id)
    {
        Note = await delivery.GetNoteAsync(id);
        QrSvg = qr.Svg(Note.Number);
        LabName = (await layout.GetAsync()).LabName;
    }
}
