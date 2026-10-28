using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Orders;

[Screen("ORD-01")]
[HasPermission(Permissions.Orders.Read)]
public sealed class IndexModel(OrderService orders) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool Open { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<CustomerOrderListItemDto> Result { get; private set; } = default!;

    public string PageUrl => $"/orders?q={Uri.EscapeDataString(Q ?? string.Empty)}&open={Open}&page={{page}}";

    public async Task OnGetAsync() => Result = await orders.ListAsync(new PageQuery(PageNumber, 20, Q), openOnly: Open);
}
