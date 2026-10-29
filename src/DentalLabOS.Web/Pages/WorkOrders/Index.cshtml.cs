using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-04")]
[HasPermission(Permissions.WorkOrders.Read)]
public sealed class IndexModel(WorkOrderService workOrders) : AppPageModel
{
    public static readonly string[] Statuses = ["Incomplete", "Ready", "InProduction", "OnHold", "ReadyForDelivery", "Delivered", "Cancelled"];

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? View { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<WorkOrderListItemDto> Result { get; private set; } = default!;

    public string PageUrl => $"/workorders?q={Uri.EscapeDataString(Q ?? string.Empty)}&status={Status}&view={View}&page={{page}}";

    public async Task OnGetAsync()
    {
        View = View is "open" or "overdue" or "all" ? View : "open";
        var status = Statuses.Contains(Status) ? Status : null;
        var filter = new WorkOrderFilter(status, Overdue: View == "overdue" ? true : null, OpenOnly: View != "all" && status is null);
        Result = await workOrders.ListAsync(new PageQuery(PageNumber, 25, Q), filter);
    }
}
