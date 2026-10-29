using DentalLabOS.Application.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages;

/// <summary>ORD-27: a barcode/QR scanner types the number and presses Enter; we open the matching work order or order.</summary>
[Screen("ORD-27")]
[HasPermission(Permissions.WorkOrders.Read)]
public sealed class ScanModel(WorkOrderService workOrders) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Code { get; set; }

    public bool Missing { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            return Page();
        }

        var hit = await workOrders.FindByNumberAsync(Code.Trim());
        if (hit is null)
        {
            Missing = true;
            return Page();
        }

        return Redirect(hit.Kind == "order" ? $"/orders/{hit.Id}" : $"/workorders/{hit.Id}");
    }
}
