using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>Customer orders (ORD-01..03): a clinic's batch of cases. Work orders are added to an order through intake.</summary>
public sealed class OrderService(
    IApplicationDbContext db, IPermissionService permissions, INumberingService numbering, IClock clock, LabCalendar calendar,
    ProductionSetupService setup, WorkOrderService workOrders)
{
    public const string DocumentType = "CustomerOrder";

    public async Task<PagedResult<CustomerOrderListItemDto>> ListAsync(PageQuery page, Guid? customerId = null, bool openOnly = false, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Orders.Read, ct);
        var query = db.CustomerOrders.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(o => o.Number.Contains(s) || db.Customers.Any(c => c.Id == o.CustomerId && c.Name.Contains(s)));
        }

        if (customerId is { } cid)
        {
            query = query.Where(o => o.CustomerId == cid);
        }

        if (openOnly)
        {
            query = query.Where(o => o.WorkOrders.Any(w => w.Status != WorkOrderStatus.Delivered && w.Status != WorkOrderStatus.Cancelled));
        }

        return await query.OrderByDescending(o => o.DocumentDate).ThenByDescending(o => o.Number)
            .Select(o => new CustomerOrderListItemDto(o.Id, o.Number, o.DocumentDate,
                db.Customers.Where(c => c.Id == o.CustomerId).Select(c => c.Name).First(),
                db.Dentists.Where(d => d.Id == o.DentistId).Select(d => d.FullName).FirstOrDefault(),
                o.Priority.ToString(), o.RequestedDeliveryDate, o.WorkOrders.Count,
                o.WorkOrders.Count(w => w.Status != WorkOrderStatus.Delivered && w.Status != WorkOrderStatus.Cancelled), o.Status.ToString()))
            .ToPagedAsync(page, ct);
    }

    public async Task<CustomerOrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Orders.Read, ct);
        var o = await db.CustomerOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(CustomerOrder));
        var today = await calendar.TodayAsync(ct);
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == o.CustomerId).Select(c => c.Name).FirstAsync(ct);
        var dentist = o.DentistId is { } did ? await db.Dentists.AsNoTracking().Where(d => d.Id == did).Select(d => d.FullName).FirstOrDefaultAsync(ct) : null;
        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == o.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var lines = await workOrders.Project(db.WorkOrders.AsNoTracking().Where(w => w.CustomerOrderId == id).OrderBy(w => w.Number), today).ToListAsync(ct);
        decimal? total = await permissions.HasAsync(Permissions.PriceLists.Read, ct)
            ? await db.WorkOrders.Where(w => w.CustomerOrderId == id && w.Status != WorkOrderStatus.Cancelled).SumAsync(w => w.QuotedPrice, ct)
            : null;
        return new CustomerOrderDto(o.Id, o.Number, o.DocumentDate, o.BranchId, branch ?? string.Empty, o.CustomerId, customer, o.DentistId, dentist,
            o.ReceivedAtUtc, o.RequestedDeliveryDate, o.Priority.ToString(), o.DeliveryInstructions, o.Notes, o.Status.ToString(), lines, total);
    }

    public async Task<CustomerOrderDto> CreateAsync(CustomerOrderCreateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Orders.Create, ct);
        var existing = await db.CustomerOrders.AsNoTracking()
            .Where(o => o.ClientOperationId == request.ClientOperationId && request.ClientOperationId != Guid.Empty).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
        if (existing is { } done)
        {
            return await GetAsync(done, ct);
        }

        var today = await calendar.TodayAsync(ct);
        var v = new Validator()
            .When(request.ClientOperationId == Guid.Empty, nameof(request.ClientOperationId), "Validation.Required")
            .When(!MasterData.TryParseEnum<PriorityLevel>(request.Priority, out var priority), nameof(request.Priority), "Validation.Required")
            .When(request.RequestedDeliveryDate is { } d && (d < today || d > today.AddYears(1)), nameof(request.RequestedDeliveryDate), "Validation.DueDate")
            .MaxLength(nameof(request.DeliveryInstructions), request.DeliveryInstructions, 1000).MaxLength(nameof(request.Notes), request.Notes, 2000);
        v.When(!await db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.IsActive, ct), nameof(request.CustomerId), "Validation.Required");
        if (request.DentistId is { } did)
        {
            v.When(!await db.Dentists.AnyAsync(x => x.Id == did && x.CustomerId == request.CustomerId, ct), nameof(request.DentistId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        var branchId = await setup.ResolveBranchAsync(request.BranchId, ct);

        var id = await db.InTransactionAsync(async innerCt =>
        {
            var order = new CustomerOrder
            {
                Number = await numbering.NextAsync(DocumentType, branchId, today, innerCt),
                BranchId = branchId,
                DocumentDate = today,
                Status = DocumentStatus.Confirmed,
                ClientOperationId = request.ClientOperationId,
                CustomerId = request.CustomerId,
                DentistId = request.DentistId,
                ReceivedAtUtc = clock.UtcNow,
                RequestedDeliveryDate = request.RequestedDeliveryDate,
                Priority = priority,
                DeliveryInstructions = MasterData.Clean(request.DeliveryInstructions),
                Notes = MasterData.Clean(request.Notes),
            };
            db.CustomerOrders.Add(order);
            await db.SaveChangesAsync(innerCt);
            return order.Id;
        }, ct);
        return await GetAsync(id, ct);
    }
}
