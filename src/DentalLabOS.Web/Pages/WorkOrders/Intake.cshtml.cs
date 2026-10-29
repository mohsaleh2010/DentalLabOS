using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.WorkOrders;

/// <summary>ORD-05 quick intake: one screen, one submit; files are uploaded after the work order exists, each on its own.</summary>
[Screen("ORD-05")]
[HasPermission(Permissions.WorkOrders.Create)]
[RequestSizeLimit(MaxUploadBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
public sealed class IntakeModel(
    WorkOrderService workOrders, OrderService orders, ProductionSetupService setup, PriceListService prices, LabCalendar calendar) : AppPageModel
{
    public const long MaxUploadBytes = 8 * FileInspector.MaxBytes;
    public static readonly string[] Priorities = ["Normal", "High", "Urgent"];

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public List<IFormFile> Files { get; set; } = [];

    public IReadOnlyList<OptionDto> Customers { get; private set; } = [];
    public IReadOnlyList<OptionDto> Dentists { get; private set; } = [];
    public IReadOnlyList<IntakeServiceDto> Services { get; private set; } = [];
    public IReadOnlyList<BranchOptionDto> Branches { get; private set; } = [];
    public CustomerOrderDto? Order { get; private set; }
    public DateOnly Today { get; private set; }

    public sealed class InputModel
    {
        public Guid ClientOperationId { get; set; } = Guid.NewGuid();
        public Guid? CustomerOrderId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? DentistId { get; set; }
        public Guid ServiceItemId { get; set; }
        public string? PatientReference { get; set; }
        public string? MaterialName { get; set; }
        public string? Shade { get; set; }
        public List<string> Teeth { get; set; } = [];
        public int? Quantity { get; set; }
        public DateOnly? DueDate { get; set; }
        public string Priority { get; set; } = "Normal";
        public string? PriorityReason { get; set; }
        public string? Instructions { get; set; }
        public Guid? BranchId { get; set; }
    }

    public async Task OnGetAsync(Guid? order, Guid? customer, Guid? service)
    {
        Input.CustomerOrderId = order;
        if (customer is { } c)
        {
            Input.CustomerId = c;
        }

        if (service is { } s)
        {
            Input.ServiceItemId = s;
        }

        await LoadAsync();
        if (Order is not null)
        {
            Input.CustomerId = Order.CustomerId;
            Input.DentistId = Order.DentistId;
            Input.Priority = Order.Priority;
            Input.DueDate = Order.RequestedDeliveryDate;
            Dentists = await workOrders.DentistOptionsAsync(Order.CustomerId);
        }
    }

    public async Task<IActionResult> OnPostAsync(string? next)
    {
        var i = Input;
        WorkOrderIntakeResult? result = null;
        var request = new WorkOrderIntakeRequest(i.CustomerId, i.ServiceItemId, i.ClientOperationId, i.DentistId, i.CustomerOrderId, i.PatientReference,
            i.MaterialName, i.Shade, i.Instructions, i.Teeth.Select(t => new ToothRequest(t)).ToList(), i.Quantity, i.DueDate, i.Priority, i.PriorityReason, i.BranchId);
        if (!await TryExecuteAsync(async () => result = await workOrders.IntakeAsync(request)))
        {
            await LoadAsync();
            return Page();
        }

        // Each file is independent: a rejected or failed file never loses the work order or the other files.
        var failed = new List<string>();
        foreach (var file in Files.Where(f => f.Length > 0))
        {
            try
            {
                await using var stream = file.OpenReadStream();
                await workOrders.UploadAsync(result!.Id, stream, file.FileName);
            }
            catch (Exception ex) when (ex is ValidationException or DentalLabOS.Domain.Common.DomainException)
            {
                failed.Add(FileInspector.SafeName(file.FileName));
            }
        }

        var notes = new List<string>();
        if (result!.Status == "Incomplete")
        {
            notes.Add(L["Intake.SavedIncomplete", result.Number].Value);
        }

        notes.AddRange(result.Warnings.Select(Coded));
        if (failed.Count > 0)
        {
            notes.Add(L["Intake.FilesFailed", string.Join("، ", failed)].Value);
        }

        TempData["toast"] = L["Intake.Saved", result.Number].Value;
        if (notes.Count > 0)
        {
            TempData["toast-error"] = string.Join(" · ", notes);
        }

        if (next == "another")
        {
            var query = result.CustomerOrderId is { } orderId ? $"order={orderId}" : $"customer={i.CustomerId}";
            return Redirect($"/workorders/intake?{query}&last={result.Id}");
        }

        return Redirect(next == "ticket" ? $"/workorders/{result.Id}/ticket" : $"/workorders/{result.Id}");
    }

    // ---- JSON for the pickers (dlos.js); the same server checks apply ----

    public async Task<IActionResult> OnGetCustomersAsync(string? q) =>
        new JsonResult((await workOrders.CustomerOptionsAsync(q)).Select(c => new { value = c.Id, label = $"{c.Name} ({c.Code})" }));

    public async Task<IActionResult> OnGetDentistsAsync(Guid customerId) =>
        new JsonResult((await workOrders.DentistOptionsAsync(customerId)).Select(d => new { value = d.Id, label = d.Name }));

    public async Task<IActionResult> OnGetQuoteAsync(Guid customerId, Guid serviceId, decimal quantity)
    {
        if (!Can(Permissions.PriceLists.Read) || customerId == Guid.Empty || serviceId == Guid.Empty)
        {
            return new JsonResult(new { });
        }

        var q = await prices.QuoteAsync(customerId, serviceId, Math.Max(1, quantity));
        return new JsonResult(new { price = Fmt.Money(q.Price), total = Fmt.Money(q.Price * Math.Max(1, quantity)), source = L[$"PriceSource.{q.Source}"].Value });
    }

    private async Task LoadAsync()
    {
        Today = await calendar.TodayAsync();
        Services = await workOrders.ServiceOptionsAsync();
        Branches = await setup.MyBranchesAsync();
        if (Input.CustomerOrderId is { } orderId && Can(Permissions.Orders.Read))
        {
            Order = await orders.GetAsync(orderId);
        }

        Customers = await workOrders.CustomerOptionsAsync(null);
        if (Input.CustomerId != Guid.Empty)
        {
            Dentists = await workOrders.DentistOptionsAsync(Input.CustomerId);
        }
    }
}
