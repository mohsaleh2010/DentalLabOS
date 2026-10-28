using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Orders;

[Screen("ORD-02")]
[HasPermission(Permissions.Orders.Create)]
public sealed class NewModel(OrderService orders, WorkOrderService workOrders, ProductionSetupService setup) : AppPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<OptionDto> Customers { get; private set; } = [];
    public IReadOnlyList<OptionDto> Dentists { get; private set; } = [];
    public IReadOnlyList<BranchOptionDto> Branches { get; private set; } = [];

    public sealed class InputModel
    {
        public Guid ClientOperationId { get; set; } = Guid.NewGuid();
        public Guid CustomerId { get; set; }
        public Guid? DentistId { get; set; }
        public Guid? BranchId { get; set; }
        public DateOnly? RequestedDeliveryDate { get; set; }
        public string Priority { get; set; } = "Normal";
        public string? DeliveryInstructions { get; set; }
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync(Guid? customerId)
    {
        if (customerId is { } c)
        {
            Input.CustomerId = c;
        }

        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        CustomerOrderDto? saved = null;
        var ok = await TryExecuteAsync(async () => saved = await orders.CreateAsync(new CustomerOrderCreateRequest(
            i.CustomerId, i.ClientOperationId, i.DentistId, i.RequestedDeliveryDate, i.Priority, i.DeliveryInstructions, i.Notes, i.BranchId)));
        if (!ok)
        {
            await LoadAsync();
            return Page();
        }

        Flash("Orders.Created", saved!.Number);
        return Redirect($"/orders/{saved.Id}");
    }

    private async Task LoadAsync()
    {
        Customers = await workOrders.CustomerOptionsAsync(null);
        if (Input.CustomerId != Guid.Empty && Customers.All(c => c.Id != Input.CustomerId))
        {
            Customers = [.. (await workOrders.CustomerOptionsAsync(null)).Where(c => c.Id == Input.CustomerId), .. Customers];
        }

        Dentists = Input.CustomerId == Guid.Empty ? [] : await workOrders.DentistOptionsAsync(Input.CustomerId);
        Branches = await setup.MyBranchesAsync();
    }
}
