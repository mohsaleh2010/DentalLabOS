using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>Read models for the production floor: board (ORD-10), department queue (ORD-11), my tasks (ORD-12), assignment (ORD-13), overdue (ORD-16) and calendar (ORD-29).</summary>
public sealed class ProductionService(
    IApplicationDbContext db, IPermissionService permissions, ICurrentUser currentUser, IClock clock, LabCalendar calendar, WorkOrderService workOrders)
{
    private const int MaxRows = 1000;

    private sealed record TaskRow(
        Guid StageId, Guid WorkOrderId, string Number, string CustomerName, string ServiceName, string StageName, StageStatus Status, Guid? DepartmentId,
        Guid? AssignedUserId, PriorityLevel Priority, DateOnly DueDate, DateTime? AvailableAtUtc, DateTime? StartedAtUtc, decimal TargetHours,
        int TeethCount, bool IsAvailable);

    /// <summary>Production departments for the board, queue and assignment filters.</summary>
    public async Task<IReadOnlyList<OptionDto>> DepartmentOptionsAsync(CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Production.OwnTasks, ct))
        {
            await permissions.DemandAsync(Permissions.Production.Read, ct);
        }

        return await db.Departments.AsNoTracking().Where(d => d.IsActive && d.IsProduction).OrderBy(d => d.Name)
            .Select(d => new OptionDto(d.Id, d.Code, d.Name)).ToListAsync(ct);
    }

    public async Task<BoardDto> BoardAsync(Guid? departmentId = null, Guid? workflowId = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Read, ct);
        var today = await calendar.TodayAsync(ct);
        var since = clock.UtcNow.AddDays(-7);
        var query = db.WorkOrders.AsNoTracking()
            .Where(w => w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Rejected && w.Status != WorkOrderStatus.Delivered
                        && (w.Status != WorkOrderStatus.ReadyForDelivery || w.CompletedAtUtc >= since));
        if (workflowId is { } wf)
        {
            query = query.Where(w => w.WorkflowDefinitionId == wf);
        }

        if (departmentId is { } dept)
        {
            query = query.Where(w => w.Stages.Any(s => s.Id == w.CurrentStageId && s.DepartmentId == dept));
        }

        var rows = await query.OrderBy(w => w.DueDate).ThenByDescending(w => w.Priority).Take(MaxRows)
            .Select(w => new
            {
                w.Id, w.Number, w.Status, w.Priority, w.DueDate, w.MissingDataNotes, Teeth = w.Teeth.Count,
                Customer = db.Customers.Where(c => c.Id == w.CustomerId).Select(c => c.Name).First(),
                Service = db.Items.Where(i => i.Id == w.ServiceItemId).Select(i => i.Name).First(),
                Stage = w.Stages.Where(s => s.Id == w.CurrentStageId).Select(s => new { s.Name, s.Step, s.Status }).FirstOrDefault(),
                Assignee = db.UserProfiles.Where(p => p.UserId == w.CurrentAssigneeUserId).Select(p => p.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var cards = rows.Select(r => (r.Status, StageName: r.Stage?.Name, StageStep: r.Stage?.Step ?? 0, Card: new BoardCardDto(
            r.Id, r.Number, r.Customer, r.Service, r.Priority.ToString(), r.DueDate,
            r.DueDate < today && r.Status != WorkOrderStatus.ReadyForDelivery, r.Stage?.Name, r.Stage?.Status.ToString(), r.Assignee,
            string.IsNullOrEmpty(r.MissingDataNotes) ? 0 : r.MissingDataNotes.Split(',').Length, r.Teeth))).ToList();

        var columns = new List<BoardColumnDto>
        {
            new("incomplete", "incomplete", "Board.Incomplete", cards.Where(c => c.Status == WorkOrderStatus.Incomplete).Select(c => c.Card).ToList()),
            new("ready", "ready", "Board.Ready", cards.Where(c => c.Status == WorkOrderStatus.Ready).Select(c => c.Card).ToList()),
        };
        columns.AddRange(cards.Where(c => c.Status is WorkOrderStatus.InProduction or WorkOrderStatus.InQuality or WorkOrderStatus.Rejected or WorkOrderStatus.Completed)
            .GroupBy(c => c.StageName ?? "-")
            .OrderBy(g => g.Min(c => c.StageStep)).ThenBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new BoardColumnDto("stage:" + g.Key, "stage", g.Key, g.Select(c => c.Card).ToList())));
        columns.Add(new("hold", "hold", "Board.OnHold", cards.Where(c => c.Status == WorkOrderStatus.OnHold).Select(c => c.Card).ToList()));
        columns.Add(new("done", "done", "Board.ReadyForDelivery", cards.Where(c => c.Status == WorkOrderStatus.ReadyForDelivery).Select(c => c.Card).ToList()));
        return new BoardDto(columns, cards.Count, cards.Count(c => c.Card.IsOverdue));
    }

    /// <summary>ORD-11: open stages of one department, workable ones first.</summary>
    public async Task<IReadOnlyList<TaskDto>> QueueAsync(Guid departmentId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Read, ct);
        return await MapAsync(await TaskRows(OpenStages().Where(s => s.DepartmentId == departmentId)).ToListAsync(ct), ct);
    }

    /// <summary>ORD-12: my stages, plus unassigned workable stages in my departments that I can pick up.</summary>
    public async Task<MyTasksDto> MyTasksAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.OwnTasks, ct);
        var me = currentUser.UserId ?? throw new ForbiddenException(Permissions.Production.OwnTasks);
        var mine = await TaskRows(OpenStages().Where(s => s.AssignedUserId == me)).ToListAsync(ct);
        var depts = await workOrders.MyDepartmentsAsync(ct);
        var available = depts.Count == 0
            ? []
            : await TaskRows(OpenStages().Where(s => s.AssignedUserId == null && s.DepartmentId != null && depts.Contains(s.DepartmentId.Value))).ToListAsync(ct);
        var firstDept = depts.Count > 0 ? depts[0] : (Guid?)null;
        var deptName = firstDept is { } d ? await db.Departments.AsNoTracking().Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        return new MyTasksDto(await MapAsync(mine, ct), await MapAsync(available.Where(r => r.IsAvailable).ToList(), ct), firstDept, deptName);
    }

    /// <summary>ORD-13: open stages to distribute (unassigned by default).</summary>
    public async Task<IReadOnlyList<TaskDto>> AssignableAsync(Guid? departmentId = null, bool includeAssigned = false, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Assign, ct);
        var query = OpenStages();
        if (departmentId is { } d)
        {
            query = query.Where(s => s.DepartmentId == d);
        }

        if (!includeAssigned)
        {
            query = query.Where(s => s.AssignedUserId == null);
        }

        return await MapAsync(await TaskRows(query).ToListAsync(ct), ct);
    }

    public async Task<OverdueDto> OverdueAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Read, ct);
        var today = await calendar.TodayAsync(ct);
        var pastDue = await workOrders.Project(db.WorkOrders.AsNoTracking().Where(WorkOrderService.OverdueAt(today)).OrderBy(w => w.DueDate).Take(MaxRows), today)
            .ToListAsync(ct);
        var running = await MapAsync(await TaskRows(OpenStages().Where(s => s.StartedAtUtc != null && s.TargetHours > 0)).ToListAsync(ct), ct);
        return new OverdueDto(pastDue, running.Where(t => t.ElapsedHours > (double)t.TargetHours).OrderByDescending(t => t.ElapsedHours - (double)t.TargetHours).ToList());
    }

    /// <summary>ORD-29: due work per day of a month against the technicians' daily capacity.</summary>
    public async Task<CalendarDto> CalendarAsync(int year, int month, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Read, ct);
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw ValidationException.For("Month", "Validation.Required");
        }

        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var today = await calendar.TodayAsync(ct);
        var rows = await db.WorkOrders.AsNoTracking().Where(w => w.DueDate >= first && w.DueDate <= last && w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Rejected)
            .GroupBy(w => w.DueDate)
            .Select(g => new
            {
                g.Key,
                Due = g.Count(),
                Done = g.Count(w => w.Status == WorkOrderStatus.ReadyForDelivery || w.Status == WorkOrderStatus.Delivered),
                Urgent = g.Count(w => w.Priority == PriorityLevel.Urgent),
            })
            .ToDictionaryAsync(x => x.Key, ct);
        var capacity = (int)Math.Round(await db.TechnicianProfiles.SumAsync(t => (decimal?)t.DifficultyCapacityPerDay, ct) ?? 0);
        var days = Enumerable.Range(0, last.Day).Select(i => first.AddDays(i)).Select(d =>
        {
            var r = rows.GetValueOrDefault(d);
            var due = r?.Due ?? 0;
            var done = r?.Done ?? 0;
            return new CalendarDayDto(d, due, done, d < today ? due - done : 0, r?.Urgent ?? 0);
        }).ToList();
        return new CalendarDto(year, month, days, capacity);
    }

    private IQueryable<WorkOrderStage> OpenStages() =>
        db.WorkOrderStages.AsNoTracking()
            .Where(s => s.Status != StageStatus.Done && s.Status != StageStatus.Skipped && s.Status != StageStatus.AwaitingQuality
                        && (s.WorkOrder.Status == WorkOrderStatus.Ready || s.WorkOrder.Status == WorkOrderStatus.InProduction
                            || s.WorkOrder.Status == WorkOrderStatus.InQuality));

    private IQueryable<TaskRow> TaskRows(IQueryable<WorkOrderStage> source) =>
        source.OrderBy(s => s.WorkOrder.DueDate).ThenByDescending(s => s.WorkOrder.Priority).ThenBy(s => s.WorkOrder.Number).ThenBy(s => s.Step).Take(MaxRows)
            .Select(s => new TaskRow(
                s.Id, s.WorkOrderId, s.WorkOrder.Number,
                db.Customers.Where(c => c.Id == s.WorkOrder.CustomerId).Select(c => c.Name).First(),
                db.Items.Where(i => i.Id == s.WorkOrder.ServiceItemId).Select(i => i.Name).First(),
                s.Name, s.Status, s.DepartmentId, s.AssignedUserId, s.WorkOrder.Priority, s.WorkOrder.DueDate, s.AvailableAtUtc, s.StartedAtUtc, s.TargetHours,
                s.WorkOrder.Teeth.Count,
                s.Step == s.WorkOrder.Stages.Where(x => x.Status != StageStatus.Done && x.Status != StageStatus.Skipped).Min(x => x.Step)));

    private async Task<IReadOnlyList<TaskDto>> MapAsync(IReadOnlyList<TaskRow> rows, CancellationToken ct)
    {
        var today = await calendar.TodayAsync(ct);
        var now = clock.UtcNow;
        var names = await workOrders.UserNamesAsync(rows.Select(r => r.AssignedUserId), ct);
        var depts = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return rows.Select(r => new TaskDto(
                r.StageId, r.WorkOrderId, r.Number, r.CustomerName, r.ServiceName, r.StageName, r.Status.ToString(), r.DepartmentId,
                r.DepartmentId is { } d ? depts.GetValueOrDefault(d) : null, r.AssignedUserId,
                r.AssignedUserId is { } u ? names.GetValueOrDefault(u) : null, r.Priority.ToString(), r.DueDate, r.DueDate < today, r.AvailableAtUtc,
                r.StartedAtUtc, r.TargetHours, r.StartedAtUtc is { } st ? Math.Round((now - st).TotalHours, 1) : 0, r.TeethCount, r.IsAvailable))
            .OrderByDescending(t => t.IsAvailable).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority == "Urgent").ToList();
    }
}
