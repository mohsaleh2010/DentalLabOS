using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Orders;

[Screen("ORD-03")]
[HasPermission(Permissions.Orders.Read)]
public sealed class DetailsModel(OrderService orders) : AppPageModel
{
    public CustomerOrderDto Order { get; private set; } = default!;

    public async Task OnGetAsync(Guid id) => Order = await orders.GetAsync(id);
}
