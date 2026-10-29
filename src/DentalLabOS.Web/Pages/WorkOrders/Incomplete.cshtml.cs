using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

[Screen("ORD-09")]
[HasPermission(Permissions.WorkOrders.Read)]
public sealed class IncompleteModel(WorkOrderService workOrders) : AppPageModel
{
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<WorkOrderListItemDto> Result { get; private set; } = default!;

    public async Task OnGetAsync() =>
        Result = await workOrders.ListAsync(new PageQuery(PageNumber, 25), new WorkOrderFilter(Incomplete: true, OpenOnly: true));
}
