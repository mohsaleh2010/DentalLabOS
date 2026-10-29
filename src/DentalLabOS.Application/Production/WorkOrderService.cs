using System.Security.Cryptography;
using System.Text.Json;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

public sealed record WorkOrderFilter(
    string? Status = null, Guid? CustomerId = null, Guid? CustomerOrderId = null, bool? Overdue = null, bool? Incomplete = null, bool OpenOnly = false);

public sealed record FileDownload(Stream Content, string FileName, string ContentType);

/// <summary>
/// Work orders (ORD-04..09, 14, 15, 27, 28): intake, prescription and its revisions, files, notes and every workflow
/// action. The workflow rules live in <see cref="WorkflowEngine"/>; this service adds permissions, data scope
/// (who may act on which stage), time entries and persistence.
/// </summary>
public sealed class WorkOrderService(
    IApplicationDbContext db,
    IPermissionService permissions,
    ICurrentUser currentUser,
    INumberingService numbering,
    IClock clock,
    LabCalendar calendar,
    PriceListService prices,
    ProductionSetupService setup,
    IFileStorage storage,
    ITenantContext tenant)
{
    public const string DocumentType = "WorkOrder";
    private const int DuplicateWindowDays = 14;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private Guid Me => currentUser.UserId ?? throw new ForbiddenException(Permissions.WorkOrders.Read);

    // ---- Lists ----

    public async Task<PagedResult<WorkOrderListItemDto>> ListAsync(PageQuery page, WorkOrderFilter filter, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        var today = await calendar.TodayAsync(ct);
        var query = db.WorkOrders.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(w => w.Number.Contains(s) || (w.PatientReference != null && w.PatientReference.Contains(s))
                                     || db.Customers.Any(c => c.Id == w.CustomerId && c.Name.Contains(s)));
        }

        if (MasterData.TryParseEnum<WorkOrderStatus>(filter.Status, out var status))
        {
            query = query.Where(w => w.Status == status);
        }

        if (filter.CustomerId is { } cid)
        {
            query = query.Where(w => w.CustomerId == cid);
        }

        if (filter.CustomerOrderId is { } oid)
        {
            query = query.Where(w => w.CustomerOrderId == oid);
        }

        if (filter.OpenOnly)
        {
            query = query.Where(w => w.Status != WorkOrderStatus.Delivered && w.Status != WorkOrderStatus.Cancelled);
        }

        if (filter.Incomplete == true)
        {
            query = query.Where(w => !w.DataComplete && w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Delivered);
        }

        if (filter.Overdue == true)
        {
            query = query.Where(OverdueAt(today));
        }

        query = page.Sort switch
        {
            "number" => query.OrderByDescending(w => w.Number),
            "received" => query.OrderByDescending(w => w.ReceivedAtUtc),
            _ => query.OrderBy(w => w.DueDate).ThenByDescending(w => w.Priority).ThenBy(w => w.Number),
        };
        return await Project(query, today).ToPagedAsync(page, ct);
    }

    internal static System.Linq.Expressions.Expression<Func<WorkOrder, bool>> OverdueAt(DateOnly today) =>
        w => w.DueDate < today && w.Status != WorkOrderStatus.ReadyForDelivery && w.Status != WorkOrderStatus.Delivered
             && w.Status != WorkOrderStatus.Cancelled;

    internal IQueryable<WorkOrderListItemDto> Project(IQueryable<WorkOrder> source, DateOnly today) =>
        source.Select(w => new WorkOrderListItemDto(
            w.Id, w.Number,
            db.Customers.Where(c => c.Id == w.CustomerId).Select(c => c.Name).First(),
            db.Dentists.Where(d => d.Id == w.DentistId).Select(d => d.FullName).FirstOrDefault(),
            db.Items.Where(i => i.Id == w.ServiceItemId).Select(i => i.Name).First(),
            w.Status.ToString(), w.Priority.ToString(), w.DueDate,
            w.DueDate < today && w.Status != WorkOrderStatus.ReadyForDelivery && w.Status != WorkOrderStatus.Delivered && w.Status != WorkOrderStatus.Cancelled,
            w.Stages.Where(st => st.Id == w.CurrentStageId).Select(st => st.Name).FirstOrDefault(),
            db.UserProfiles.Where(p => p.UserId == w.CurrentAssigneeUserId).Select(p => p.FullName).FirstOrDefault(),
            w.DataComplete, w.Kind.ToString(), w.Quantity, w.ReceivedAtUtc, w.PatientReference));

    // ---- Intake (ORD-05) ----

    public async Task<WorkOrderIntakeResult> IntakeAsync(WorkOrderIntakeRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Create, ct);

        // A retried submit (double click, flaky network) returns the first result.
        var previous = await db.WorkOrders.AsNoTracking().Where(w => w.ClientOperationId == request.ClientOperationId && request.ClientOperationId != Guid.Empty)
            .Select(w => new { w.Id, w.Number, w.Status, w.MissingDataNotes, w.CustomerOrderId }).FirstOrDefaultAsync(ct);
        if (previous is not null)
        {
            return new WorkOrderIntakeResult(previous.Id, previous.Number, previous.Status.ToString(), SplitMissing(previous.MissingDataNotes), [], previous.CustomerOrderId);
        }

        var v = new Validator().When(request.ClientOperationId == Guid.Empty, nameof(request.ClientOperationId), "Validation.Required");
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.IsActive, ct);
        v.When(customer is null, nameof(request.CustomerId), "Validation.Required");
        var service = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ServiceItemId && i.IsActive, ct);
        v.When(service is null || !Item.IsServiceType(service.Type), nameof(request.ServiceItemId), "Validation.Required");
        var teeth = ValidatePrescription(v, request.DentistId, request.Teeth, request.Quantity, request.PatientReference, request.MaterialName, request.Shade,
            request.Instructions, request.PriorityReason, request.Priority, out var priority);
        if (request.DentistId is { } did)
        {
            v.When(!await db.Dentists.AnyAsync(d => d.Id == did && d.CustomerId == request.CustomerId && d.IsActive, ct), nameof(request.DentistId), "Validation.Required");
        }

        if (request.CustomerOrderId is { } orderId)
        {
            v.When(!await db.CustomerOrders.AnyAsync(o => o.Id == orderId && o.CustomerId == request.CustomerId && o.Status != DocumentStatus.Voided, ct),
                nameof(request.CustomerOrderId), "Validation.OrderCustomer");
        }

        var today = await calendar.TodayAsync(ct);
        v.When(request.DueDate is { } due && (due < today || due > today.AddYears(1)), nameof(request.DueDate), "Validation.DueDate");
        var workflow = service is null ? null : await LoadWorkflowForAsync(service, ct);
        v.When(service is not null && workflow is null, nameof(request.ServiceItemId), "Validation.NoWorkflow");
        v.ThrowIfInvalid();

        var branchId = await setup.ResolveBranchAsync(request.BranchId, ct);
        var quantity = request.Quantity ?? Math.Max(1, teeth.Count(t => t != Fdi.UpperArch && t != Fdi.LowerArch));
        var price = await prices.ResolveAsync(customer!.Id, service!, quantity, today, ct);
        var warnings = await DuplicateWarningsAsync(customer.Id, request.PatientReference, teeth, null, ct);

        return await db.InTransactionAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            var wo = new WorkOrder
            {
                Number = await numbering.NextAsync(DocumentType, branchId, today, innerCt),
                BranchId = branchId,
                CustomerOrderId = request.CustomerOrderId,
                CustomerId = customer.Id,
                DentistId = request.DentistId,
                ServiceItemId = service!.Id,
                Priority = priority,
                PriorityReason = MasterData.Clean(request.PriorityReason),
                PatientReference = MasterData.Clean(request.PatientReference),
                MaterialName = MasterData.Clean(request.MaterialName),
                Shade = MasterData.Clean(request.Shade),
                Instructions = MasterData.Clean(request.Instructions),
                Quantity = quantity,
                ReceivedAtUtc = now,
                DueDate = request.DueDate ?? LabCalendar.SuggestDueDate(today, service.StandardLeadHours),
                UnitPrice = price.Price,
                QuotedPrice = price.Price * quantity,
                ClientOperationId = request.ClientOperationId,
                Status = WorkOrderStatus.Incomplete,
            };
            SetTeeth(wo, teeth, request.Teeth);
            WorkflowEngine.CopyStages(wo, workflow!);
            db.WorkOrders.Add(wo);
            db.StageTransitions.Add(new StageTransitionLog
            {
                WorkOrderId = wo.Id, Action = TransitionAction.Created, FromStatus = WorkOrderStatus.Draft, ToStatus = WorkOrderStatus.Incomplete, UserId = Me, AtUtc = now,
            });
            var missing = WorkflowEngine.MissingData(workflow, wo, 0);
            if (WorkflowEngine.ApplyDataCheck(wo, missing, Me, now) is { } log)
            {
                db.StageTransitions.Add(log);
            }

            await db.SaveChangesAsync(innerCt);
            return new WorkOrderIntakeResult(wo.Id, wo.Number, wo.Status.ToString(), missing.Select(m => m.ToString()).ToList(), warnings, wo.CustomerOrderId);
        }, ct);
    }

    /// <summary>Possible duplicate (ORD-05): same clinic and patient with a shared tooth in the last two weeks. A warning, not a block.</summary>
    public async Task<IReadOnlyList<string>> DuplicateWarningsAsync(Guid customerId, string? patientReference, IReadOnlyCollection<string> teeth, Guid? exceptId, CancellationToken ct)
    {
        var patient = MasterData.Clean(patientReference);
        if (patient is null || teeth.Count == 0)
        {
            return [];
        }

        var since = clock.UtcNow.AddDays(-DuplicateWindowDays);
        var codes = teeth.ToList();
        var numbers = await db.WorkOrders.AsNoTracking()
            .Where(w => w.CustomerId == customerId && w.PatientReference == patient && w.ReceivedAtUtc >= since && w.Status != WorkOrderStatus.Cancelled
                        && w.Id != exceptId && w.Teeth.Any(t => codes.Contains(t.ToothCode)))
            .Select(w => w.Number).Take(5).ToListAsync(ct);
        return numbers.Select(n => $"WorkOrder.PossibleDuplicate:{n}").ToList();
    }

    // ---- Intake pickers (need only workorders.create, so reception doesn't depend on catalog permissions) ----

    public async Task<IReadOnlyList<OptionDto>> CustomerOptionsAsync(string? search, CancellationToken ct = default)
    {
        await DemandIntakeAsync(ct);
        var query = db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (MasterData.Clean(search) is { } s)
        {
            query = query.Where(c => c.Name.Contains(s) || c.Code.Contains(s) || c.Mobile == s || c.Phone == s);
        }

        return await query.OrderBy(c => c.Name).Take(100).Select(c => new OptionDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OptionDto>> DentistOptionsAsync(Guid customerId, CancellationToken ct = default)
    {
        await DemandIntakeAsync(ct);
        return await db.Dentists.AsNoTracking().Where(d => d.CustomerId == customerId && d.IsActive).OrderBy(d => d.FullName)
            .Select(d => new OptionDto(d.Id, d.Specialty ?? string.Empty, d.FullName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IntakeServiceDto>> ServiceOptionsAsync(CancellationToken ct = default)
    {
        await DemandIntakeAsync(ct);
        var workflows = await db.Workflows.AsNoTracking().Where(w => w.IsActive).ToListAsync(ct);
        var fallback = workflows.FirstOrDefault(w => w.IsDefault);
        var services = await db.Items.AsNoTracking().Where(i => i.IsActive && (i.Type == ItemType.Service || i.Type == ItemType.OutsourcedService))
            .OrderBy(i => i.Name).Select(i => new { i.Id, i.Sku, i.Name, i.StandardLeadHours, i.DefaultWorkflowId }).ToListAsync(ct);
        return services.Select(i =>
        {
            var wf = workflows.FirstOrDefault(w => w.Id == i.DefaultWorkflowId) ?? fallback;
            var required = WorkflowEngine.MissingData(wf, new WorkOrder(), 0).Select(f => f.ToString()).ToList();
            return new IntakeServiceDto(i.Id, i.Sku, i.Name, i.StandardLeadHours, wf?.Name, required);
        }).ToList();
    }

    private async Task DemandIntakeAsync(CancellationToken ct)
    {
        if (!await permissions.HasAsync(Permissions.WorkOrders.Update, ct))
        {
            await permissions.DemandAsync(Permissions.WorkOrders.Create, ct);
        }
    }

    // ---- Details ----

    public async Task<WorkOrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        var wo = await db.WorkOrders.AsNoTracking()
                     .Include(w => w.Teeth).Include(w => w.Stages).ThenInclude(s => s.TimeEntries)
                     .Include(w => w.Attachments).ThenInclude(a => a.File).Include(w => w.Notes)
                     .FirstOrDefaultAsync(w => w.Id == id, ct) ?? throw new NotFoundException(nameof(WorkOrder));
        var today = await calendar.TodayAsync(ct);
        var now = clock.UtcNow;

        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == wo.CustomerId).Select(c => c.Name).FirstAsync(ct);
        var dentist = wo.DentistId is { } did ? await db.Dentists.AsNoTracking().Where(d => d.Id == did).Select(d => d.FullName).FirstOrDefaultAsync(ct) : null;
        var service = await db.Items.AsNoTracking().Where(i => i.Id == wo.ServiceItemId).Select(i => new { i.Sku, i.Name }).FirstAsync(ct);
        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == wo.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var orderNumber = wo.CustomerOrderId is { } oid ? await db.CustomerOrders.AsNoTracking().Where(o => o.Id == oid).Select(o => o.Number).FirstOrDefaultAsync(ct) : null;
        var workflowName = wo.WorkflowDefinitionId is { } wid ? await db.Workflows.AsNoTracking().Where(x => x.Id == wid).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var deptIds = wo.Stages.Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var depts = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var hold = await db.Holds.AsNoTracking().Where(h => h.WorkOrderId == id && h.ResumedAtUtc == null).OrderByDescending(h => h.HeldAtUtc).FirstOrDefaultAsync(ct);
        var holdReason = hold is null ? null : await db.ReasonCodes.AsNoTracking().Where(r => r.Id == hold.ReasonCodeId).Select(r => r.Name).FirstOrDefaultAsync(ct);

        var userIds = wo.Stages.Select(s => s.AssignedUserId).Concat([wo.CurrentAssigneeUserId, hold?.HeldByUserId])
            .Concat(wo.Attachments.Select(a => a.CreatedByUserId)).Concat(wo.Notes.Select(n => n.CreatedByUserId));
        var names = await UserNamesAsync(userIds, ct);
        var showPrice = await permissions.HasAsync(Permissions.PriceLists.Read, ct);
        var available = WorkflowEngine.AvailableStages(wo).Select(s => s.Id).ToHashSet();

        var stages = wo.Stages.OrderBy(s => s.Step).ThenBy(s => s.Name, StringComparer.Ordinal).Select(s =>
        {
            var worked = s.TimeEntries.Sum(e => e.Minutes(now));
            double? waiting = s.AvailableAtUtc is { } av ? ((s.StartedAtUtc ?? (s.IsFinished ? s.CompletedAtUtc : null) ?? now) - av).TotalMinutes : null;
            var elapsedHours = s.StartedAtUtc is { } st ? ((s.CompletedAtUtc ?? now) - st).TotalHours : 0;
            return new WorkOrderStageDto(s.Id, s.Step, s.Name, s.DepartmentId is { } d ? depts.GetValueOrDefault(d) : null, s.DepartmentId, s.Status.ToString(),
                available.Contains(s.Id), s.AssignedUserId, Name(names, s.AssignedUserId), s.TargetHours, s.AvailableAtUtc, s.StartedAtUtc, s.CompletedAtUtc,
                Math.Round(worked, 1), waiting is null ? null : Math.Round(Math.Max(0, waiting.Value), 1),
                s.TargetHours > 0 && elapsedHours > (double)s.TargetHours && s.Status != StageStatus.Skipped, s.SkipReason, s.RequiresQualityPass);
        }).ToList();

        var currentStage = wo.Stages.FirstOrDefault(s => s.Id == wo.CurrentStageId);
        var overdue = wo.IsOverdue(today);
        return new WorkOrderDto(
            wo.Id, wo.Number, wo.BranchId, branch ?? string.Empty, wo.CustomerOrderId, orderNumber, wo.CustomerId, customer, wo.DentistId, dentist,
            wo.ServiceItemId, service.Sku, service.Name, wo.Kind.ToString(), wo.Status.ToString(), wo.Priority.ToString(), wo.PriorityReason,
            wo.PatientReference, wo.MaterialName, wo.Shade, wo.Instructions, wo.Quantity, wo.ReceivedAtUtc, wo.DueDate, overdue,
            overdue ? today.DayNumber - wo.DueDate.DayNumber : 0, wo.DataComplete, SplitMissing(wo.MissingDataNotes),
            showPrice ? wo.UnitPrice : null, showPrice ? wo.QuotedPrice : null, workflowName, wo.WorkflowVersion, wo.CurrentStageId, currentStage?.Name,
            Name(names, wo.CurrentAssigneeUserId), wo.RevisionNumber, wo.CancelReason, wo.CompletedAtUtc,
            wo.Teeth.OrderBy(t => Fdi.Order(t.ToothCode)).Select(t => new ToothDto(t.ToothCode, t.Treatment)).ToList(),
            stages,
            wo.Attachments.OrderByDescending(a => a.CreatedAtUtc).Select(a => new AttachmentDto(a.Id, a.File.OriginalName, a.Kind, a.File.ContentType,
                a.File.SizeBytes, a.VisibleToDentist, a.CreatedAtUtc, Name(names, a.CreatedByUserId))).ToList(),
            wo.Notes.OrderByDescending(n => n.CreatedAtUtc).Select(n => new NoteDto(n.Id, n.Text, n.IsInternal, n.CreatedAtUtc, Name(names, n.CreatedByUserId))).ToList(),
            hold is null ? null : new HoldDto(hold.Id, holdReason ?? string.Empty, hold.Notes, hold.HeldAtUtc, Name(names, hold.HeldByUserId)),
            RowVersions.Encode(wo.RowVersion));
    }

    public async Task<IReadOnlyList<TimelineEntryDto>> TimelineAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        await EnsureExistsAsync(id, ct);
        var logs = await db.StageTransitions.AsNoTracking().Where(l => l.WorkOrderId == id).OrderByDescending(l => l.AtUtc).ThenByDescending(l => l.CreatedAtUtc).ToListAsync(ct);
        var stageNames = await db.WorkOrderStages.AsNoTracking().Where(s => s.WorkOrderId == id).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var names = await UserNamesAsync(logs.Select(l => (Guid?)l.UserId), ct);
        return logs.Select(l => new TimelineEntryDto(l.AtUtc, l.Action.ToString(), l.FromStatus.ToString(), l.ToStatus.ToString(),
            (l.Action is TransitionAction.Completed or TransitionAction.Skipped ? l.FromStageId : l.ToStageId) is { } sid ? stageNames.GetValueOrDefault(sid) : null,
            Name(names, l.UserId), l.Reason)).ToList();
    }

    public async Task<IReadOnlyList<RevisionDto>> RevisionsAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        await EnsureExistsAsync(id, ct);
        var revisions = await db.WorkOrderRevisions.AsNoTracking().Where(r => r.WorkOrderId == id).OrderByDescending(r => r.RevisionNumber).ToListAsync(ct);
        var names = await UserNamesAsync(revisions.Select(r => (Guid?)r.ChangedByUserId), ct);
        return revisions.Select(r => new RevisionDto(r.Id, r.RevisionNumber, r.ChangeSummary, r.CreatedAtUtc, Name(names, r.ChangedByUserId), r.PrescriptionJson)).ToList();
    }

    /// <summary>ORD-27: a scanned or typed work-order or order number.</summary>
    public async Task<ScanResultDto?> FindByNumberAsync(string? number, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        var n = number?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(n) || n.Length > 30)
        {
            return null;
        }

        var wo = await db.WorkOrders.AsNoTracking().Where(w => w.Number == n).Select(w => new { w.Id, w.Number }).FirstOrDefaultAsync(ct);
        if (wo is not null)
        {
            return new ScanResultDto("workorder", wo.Id, wo.Number);
        }

        var order = await db.CustomerOrders.AsNoTracking().Where(o => o.Number == n).Select(o => new { o.Id, o.Number }).FirstOrDefaultAsync(ct);
        return order is null ? null : new ScanResultDto("order", order.Id, order.Number);
    }

    // ---- Prescription changes (ORD-06/08) ----

    public async Task UpdateAsync(Guid id, WorkOrderUpdateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Update, ct);
        var wo = await LoadAsync(id, ct);
        if (!wo.IsOpen || wo.Status == WorkOrderStatus.ReadyForDelivery)
        {
            throw new DomainException("Errors.WorkOrder.Locked");
        }

        var v = new Validator();
        var teeth = ValidatePrescription(v, request.DentistId, request.Teeth, request.Quantity, request.PatientReference, request.MaterialName, request.Shade,
            request.Instructions, request.PriorityReason, request.Priority, out var priority);
        if (request.DentistId is { } did)
        {
            v.When(!await db.Dentists.AnyAsync(d => d.Id == did && d.CustomerId == wo.CustomerId, ct), nameof(request.DentistId), "Validation.Required");
        }

        var today = await calendar.TodayAsync(ct);
        v.When(request.DueDate != wo.DueDate && (request.DueDate < today || request.DueDate > today.AddYears(1)), nameof(request.DueDate), "Validation.DueDate");
        v.When(wo.ProductionStarted && string.IsNullOrWhiteSpace(request.ChangeSummary), nameof(request.ChangeSummary), "Validation.ChangeSummary");
        v.MaxLength(nameof(request.ChangeSummary), request.ChangeSummary, 1000);
        v.ThrowIfInvalid();

        db.SetOriginalRowVersion(wo, request.RowVersion);
        var before = Snapshot(wo);
        wo.DentistId = request.DentistId;
        wo.PatientReference = MasterData.Clean(request.PatientReference);
        wo.MaterialName = MasterData.Clean(request.MaterialName);
        wo.Shade = MasterData.Clean(request.Shade);
        wo.Instructions = MasterData.Clean(request.Instructions);
        wo.Quantity = request.Quantity;
        wo.QuotedPrice = wo.UnitPrice * request.Quantity;
        wo.DueDate = request.DueDate;
        wo.Priority = priority;
        wo.PriorityReason = MasterData.Clean(request.PriorityReason);
        SetTeeth(wo, teeth, request.Teeth);

        var now = clock.UtcNow;
        if (JsonSerializer.Serialize(Snapshot(wo), Json) != JsonSerializer.Serialize(before, Json))
        {
            // The previous prescription is kept; nothing is replaced silently.
            db.WorkOrderRevisions.Add(new WorkOrderRevision
            {
                WorkOrderId = wo.Id, RevisionNumber = wo.RevisionNumber, PrescriptionJson = JsonSerializer.Serialize(before, Json),
                ChangeSummary = MasterData.Clean(request.ChangeSummary) ?? "-", ChangedByUserId = Me,
            });
            wo.RevisionNumber++;
            db.StageTransitions.Add(new StageTransitionLog
            {
                WorkOrderId = wo.Id, Action = TransitionAction.Revised, FromStatus = wo.Status, ToStatus = wo.Status, FromStageId = wo.CurrentStageId,
                ToStageId = wo.CurrentStageId, UserId = Me, Reason = MasterData.Clean(request.ChangeSummary), AtUtc = now,
            });
        }

        await RecheckDataAsync(wo, now, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddNoteAsync(Guid id, NoteRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        if (!await permissions.HasAsync(Permissions.WorkOrders.Update, ct))
        {
            await permissions.DemandAsync(Permissions.WorkOrders.Transition, ct);
        }

        new Validator().Required(nameof(request.Text), request.Text).MaxLength(nameof(request.Text), request.Text, 2000).ThrowIfInvalid();
        await EnsureExistsAsync(id, ct);
        db.WorkOrderNotes.Add(new WorkOrderNote { WorkOrderId = id, Text = request.Text.Trim(), IsInternal = request.IsInternal });
        await db.SaveChangesAsync(ct);
    }

    // ---- Files ----

    public async Task<AttachmentDto> UploadAsync(Guid id, Stream content, string fileName, CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.WorkOrders.Update, ct))
        {
            await permissions.DemandAsync(Permissions.WorkOrders.Create, ct);
        }

        var wo = await LoadAsync(id, ct);
        if (!wo.IsOpen)
        {
            throw new DomainException("Errors.WorkOrder.Locked");
        }

        if (await db.WorkOrderAttachments.CountAsync(a => a.WorkOrderId == id, ct) >= FileInspector.MaxPerWorkOrder)
        {
            throw ValidationException.For("File", "Validation.TooManyFiles");
        }

        using var buffer = new MemoryStream();
        await CopyLimitedAsync(content, buffer, FileInspector.MaxBytes, ct);
        var bytes = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        var inspected = FileInspector.Inspect(bytes.Span, fileName) ?? throw ValidationException.For("File", "Validation.FileType");

        var key = $"{tenant.TenantId:N}/{clock.UtcNow:yyyy/MM}/{Guid.CreateVersion7():N}{inspected.Extension}";
        var file = new FileMetadata
        {
            OriginalName = FileInspector.SafeName(fileName), StorageKey = key, ContentType = inspected.ContentType, SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes.Span)),

            // The signature check above is the content scan available today; an antivirus hook can flip this later.
            ScanStatus = "Clean",
        };
        var attachment = new WorkOrderAttachment { WorkOrderId = id, FileId = file.Id, File = file, Kind = inspected.Kind };

        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);
        try
        {
            db.Files.Add(file);
            db.WorkOrderAttachments.Add(attachment);
            wo.Attachments.Add(attachment);
            await RecheckDataAsync(wo, clock.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return new AttachmentDto(attachment.Id, file.OriginalName, attachment.Kind, file.ContentType, file.SizeBytes, false, attachment.CreatedAtUtc, null);
    }

    public async Task<FileDownload> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        var a = await db.WorkOrderAttachments.AsNoTracking().Include(x => x.File).FirstOrDefaultAsync(x => x.Id == attachmentId && x.WorkOrderId == id, ct)
                ?? throw new NotFoundException(nameof(WorkOrderAttachment));
        return new FileDownload(await storage.OpenReadAsync(a.File.StorageKey, ct), a.File.OriginalName, a.File.ContentType);
    }

    /// <summary>Soft delete: the file stays in storage for the audit trail.</summary>
    public async Task DeleteAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Update, ct);
        var wo = await LoadAsync(id, ct);
        var a = wo.Attachments.FirstOrDefault(x => x.Id == attachmentId) ?? throw new NotFoundException(nameof(WorkOrderAttachment));
        a.IsDeleted = true;
        a.DeletedAtUtc = clock.UtcNow;
        a.DeletedByUserId = Me;
        wo.Attachments.Remove(a);
        await RecheckDataAsync(wo, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---- Workflow actions (ORD-11..15, 28) ----

    public async Task StartStageAsync(Guid id, Guid stageId, CancellationToken ct = default)
    {
        var rate = await MyLaborRateAsync(ct);
        await StageActionAsync(id, stageId, false, (wo, stage, now) =>
        {
            var log = WorkflowEngine.Start(wo, stage, Me, now);
            stage.TimeEntries.Add(new TaskTimeEntry
            {
                WorkOrderStageId = stage.Id, WorkOrderId = wo.Id, UserId = Me, StartedAtUtc = now, LaborRateSnapshot = rate(stage.DepartmentId),
            });
            return log;
        }, ct);
    }

    /// <summary>The hourly rate frozen on each time entry: the technician's own rate, else their stage department's rate.</summary>
    private async Task<Func<Guid?, decimal>> MyLaborRateAsync(CancellationToken ct)
    {
        var me = Me;
        var own = await (from t in db.TechnicianProfiles.AsNoTracking()
                         join p in db.UserProfiles.AsNoTracking() on t.UserProfileId equals p.Id
                         where p.UserId == me
                         select (decimal?)t.LaborRatePerHour).FirstOrDefaultAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => d.LaborRatePerHour != null)
            .ToDictionaryAsync(d => d.Id, d => d.LaborRatePerHour!.Value, ct);
        return dept => own is > 0 ? own.Value : dept is { } d ? departments.GetValueOrDefault(d) : 0;
    }

    public Task PauseStageAsync(Guid id, Guid stageId, string? reason, CancellationToken ct = default) =>
        StageActionAsync(id, stageId, false, (wo, stage, now) =>
        {
            CloseEntries(stage, now);
            return WorkflowEngine.Pause(wo, stage, Me, now, reason);
        }, ct);

    public Task CompleteStageAsync(Guid id, Guid stageId, CancellationToken ct = default) =>
        StageActionAsync(id, stageId, false, (wo, stage, now) =>
        {
            CloseEntries(stage, now);
            return WorkflowEngine.Complete(wo, stage, Me, now);
        }, ct);

    public Task SkipStageAsync(Guid id, Guid stageId, string? reason, CancellationToken ct = default) =>
        StageActionAsync(id, stageId, true, (wo, stage, now) => WorkflowEngine.Skip(wo, stage, Me, now, reason), ct);

    public Task MoveBackAsync(Guid id, Guid stageId, string? reason, CancellationToken ct = default) =>
        StageActionAsync(id, stageId, true, (wo, stage, now) =>
        {
            foreach (var s in wo.Stages)
            {
                CloseEntries(s, now);
            }

            return WorkflowEngine.MoveBack(wo, stage, Me, now, reason);
        }, ct);

    public async Task HoldAsync(Guid id, HoldRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Hold, ct);
        new Validator().MaxLength(nameof(request.Notes), request.Notes, 500)
            .When(!await db.ReasonCodes.AnyAsync(r => r.Id == request.ReasonCodeId && r.Scope == ReasonScope.Hold && r.IsActive, ct), nameof(request.ReasonCodeId), "Validation.Required")
            .ThrowIfInvalid();
        var wo = await LoadAsync(id, ct);
        var now = clock.UtcNow;
        foreach (var s in wo.Stages)
        {
            CloseEntries(s, now);
        }

        var (hold, log) = WorkflowEngine.Hold(wo, request.ReasonCodeId, request.Notes, Me, now);
        db.Holds.Add(hold);
        db.StageTransitions.Add(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReleaseAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Hold, ct);
        var wo = await LoadAsync(id, ct);
        var hold = await db.Holds.Where(h => h.WorkOrderId == id && h.ResumedAtUtc == null).OrderByDescending(h => h.HeldAtUtc).FirstOrDefaultAsync(ct)
                   ?? throw new DomainException("Errors.WorkOrder.NotOnHold");
        db.StageTransitions.Add(WorkflowEngine.Release(wo, hold, Me, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid id, CancelRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Cancel, ct);
        var reason = await db.ReasonCodes.AsNoTracking().Where(r => r.Id == request.ReasonCodeId && r.Scope == ReasonScope.Cancellation && r.IsActive)
            .Select(r => r.Name).FirstOrDefaultAsync(ct);
        new Validator().MaxLength(nameof(request.Notes), request.Notes, 400).When(reason is null, nameof(request.ReasonCodeId), "Validation.Required").ThrowIfInvalid();
        var wo = await LoadAsync(id, ct);
        if (wo.InvoiceLineId is not null)
        {
            throw new DomainException("Errors.WorkOrder.Invoiced");
        }

        var now = clock.UtcNow;
        foreach (var s in wo.Stages)
        {
            CloseEntries(s, now);
        }

        var text = MasterData.Clean(request.Notes) is { } notes ? $"{reason} — {notes}" : reason;
        db.StageTransitions.Add(WorkflowEngine.Cancel(wo, Me, now, text));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>ORD-13: assign (or unassign with a null user) many stages at once. Finished stages are skipped.</summary>
    public async Task<AssignResultDto> AssignAsync(AssignRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Assign, ct);
        var ids = (request.StageIds ?? []).Distinct().Take(500).ToList();
        if (request.UserId is { } uid && !await db.UserProfiles.AnyAsync(p => p.UserId == uid && p.IsActive, ct))
        {
            throw ValidationException.For(nameof(request.UserId), "Validation.Required");
        }

        var stages = await db.WorkOrderStages.Include(s => s.WorkOrder).Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var now = clock.UtcNow;
        var assigned = 0;
        foreach (var s in stages.Where(s => !s.IsFinished && s.WorkOrder.IsOpen))
        {
            s.AssignedUserId = request.UserId;
            if (s.WorkOrder.CurrentStageId == s.Id)
            {
                s.WorkOrder.CurrentAssigneeUserId = request.UserId;
            }

            db.StageTransitions.Add(new StageTransitionLog
            {
                WorkOrderId = s.WorkOrderId, Action = TransitionAction.Assigned, FromStatus = s.WorkOrder.Status, ToStatus = s.WorkOrder.Status,
                FromStageId = s.Id, ToStageId = s.Id, UserId = Me, AtUtc = now,
            });
            assigned++;
        }

        await db.SaveChangesAsync(ct);
        return new AssignResultDto(assigned, ids.Count - assigned);
    }

    /// <summary>Departments whose unassigned work the current user may pick up: their technician profile plus department scopes.</summary>
    public async Task<IReadOnlyList<Guid>> MyDepartmentsAsync(CancellationToken ct = default)
    {
        var me = Me;
        var profileId = await db.UserProfiles.AsNoTracking().Where(p => p.UserId == me).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (profileId is null)
        {
            return [];
        }

        var tech = await db.TechnicianProfiles.AsNoTracking().Where(t => t.UserProfileId == profileId).Select(t => t.DepartmentId).ToListAsync(ct);
        var scopes = await db.UserDepartmentScopes.AsNoTracking().Where(s => s.UserProfileId == profileId).Select(s => s.DepartmentId).ToListAsync(ct);
        return tech.Concat(scopes).Distinct().ToList();
    }

    // ---- Internals ----

    private async Task StageActionAsync(Guid id, Guid stageId, bool supervisorOnly, Func<WorkOrder, WorkOrderStage, DateTime, StageTransitionLog> action, CancellationToken ct)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Transition, ct);
        if (supervisorOnly)
        {
            await permissions.DemandAsync(Permissions.Production.Assign, ct);
        }

        var wo = await LoadAsync(id, ct);
        var stage = wo.Stages.FirstOrDefault(s => s.Id == stageId) ?? throw new NotFoundException(nameof(WorkOrderStage));
        if (!supervisorOnly && !await CanWorkOnAsync(stage, ct))
        {
            throw new ForbiddenException(Permissions.Production.Assign);
        }

        db.StageTransitions.Add(action(wo, stage, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Data scope (plan 5.2): supervisors act on any stage; others on their own stages or unassigned work in their departments.</summary>
    private async Task<bool> CanWorkOnAsync(WorkOrderStage stage, CancellationToken ct)
    {
        if (await permissions.HasAsync(Permissions.Production.Assign, ct) || stage.AssignedUserId == Me)
        {
            return true;
        }

        if (stage.AssignedUserId is not null)
        {
            return false;
        }

        return stage.DepartmentId is null || (await MyDepartmentsAsync(ct)).Contains(stage.DepartmentId.Value);
    }

    private static void CloseEntries(WorkOrderStage stage, DateTime now)
    {
        foreach (var e in stage.TimeEntries.Where(e => e.EndedAtUtc is null))
        {
            e.EndedAtUtc = now;
        }
    }

    private async Task<WorkOrder> LoadAsync(Guid id, CancellationToken ct) =>
        await db.WorkOrders.Include(w => w.Teeth).Include(w => w.Attachments)
            .Include(w => w.Stages).ThenInclude(s => s.TimeEntries)
            .FirstOrDefaultAsync(w => w.Id == id, ct) ?? throw new NotFoundException(nameof(WorkOrder));

    private async Task EnsureExistsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(w => w.Id == id, ct))
        {
            throw new NotFoundException(nameof(WorkOrder));
        }
    }

    private async Task RecheckDataAsync(WorkOrder wo, DateTime now, CancellationToken ct)
    {
        var workflow = wo.WorkflowDefinitionId is { } wid ? await db.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid, ct) : null;
        var missing = WorkflowEngine.MissingData(workflow, wo, wo.Attachments.Count(a => !a.IsDeleted));
        if (WorkflowEngine.ApplyDataCheck(wo, missing, Me, now) is { } log)
        {
            db.StageTransitions.Add(log);
        }
    }

    private async Task<WorkflowDefinition?> LoadWorkflowForAsync(Item service, CancellationToken ct)
    {
        var query = db.Workflows.AsNoTracking().Include(w => w.Stages).Where(w => w.IsActive);
        return (service.DefaultWorkflowId is { } id ? await query.FirstOrDefaultAsync(w => w.Id == id, ct) : null)
               ?? await query.FirstOrDefaultAsync(w => w.IsDefault, ct);
    }

    private static List<string> ValidatePrescription(
        Validator v, Guid? dentistId, IReadOnlyList<ToothRequest>? teeth, int? quantity, string? patient, string? material, string? shade, string? instructions,
        string? priorityReason, string? priorityText, out PriorityLevel priority)
    {
        _ = dentistId;
        var codes = new List<string>();
        foreach (var t in teeth ?? [])
        {
            if (Fdi.Normalize(t.Code) is { } c)
            {
                if (!codes.Contains(c))
                {
                    codes.Add(c);
                }
            }
            else
            {
                v.Add("Teeth", "Validation.ToothCode");
            }

            v.MaxLength("Teeth", t.Treatment, 100);
        }

        if (!MasterData.TryParseEnum(priorityText, out priority))
        {
            v.Add("Priority", "Validation.Required");
        }

        v.Range("Quantity", quantity ?? 1, 1, 64)
            .MaxLength("PatientReference", patient, 100).MaxLength("MaterialName", material, 150).MaxLength("Shade", shade, 50)
            .MaxLength("Instructions", instructions, 4000).MaxLength("PriorityReason", priorityReason, 300)
            .When(priority != PriorityLevel.Normal && string.IsNullOrWhiteSpace(priorityReason), "PriorityReason", "Validation.PriorityReason");
        return codes;
    }

    private static void SetTeeth(WorkOrder wo, List<string> codes, IReadOnlyList<ToothRequest>? requested)
    {
        var treatments = (requested ?? []).Where(t => Fdi.Normalize(t.Code) is not null)
            .GroupBy(t => Fdi.Normalize(t.Code)!).ToDictionary(g => g.Key, g => MasterData.Clean(g.First().Treatment));
        foreach (var gone in wo.Teeth.Where(t => !codes.Contains(t.ToothCode)).ToList())
        {
            wo.Teeth.Remove(gone);
        }

        foreach (var code in codes)
        {
            var row = wo.Teeth.FirstOrDefault(t => t.ToothCode == code);
            if (row is null)
            {
                wo.Teeth.Add(new WorkOrderTooth { WorkOrderId = wo.Id, ToothCode = code, Treatment = treatments.GetValueOrDefault(code) });
            }
            else
            {
                row.Treatment = treatments.GetValueOrDefault(code);
            }
        }
    }

    private static object Snapshot(WorkOrder wo) => new
    {
        wo.DentistId, wo.PatientReference, wo.MaterialName, wo.Shade, wo.Instructions, wo.Quantity, wo.DueDate, Priority = wo.Priority.ToString(),
        wo.PriorityReason, Teeth = wo.Teeth.OrderBy(t => Fdi.Order(t.ToothCode)).Select(t => new { t.ToothCode, t.Treatment }).ToList(),
    };

    private static async Task CopyLimitedAsync(Stream source, Stream target, long max, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > max)
            {
                throw ValidationException.For("File", "Validation.FileSize");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private static IReadOnlyList<string> SplitMissing(string? notes) =>
        string.IsNullOrEmpty(notes) ? [] : notes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return list.Count == 0
            ? []
            : await db.UserProfiles.AsNoTracking().Where(p => list.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.FullName, ct);
    }

    private static string? Name(IReadOnlyDictionary<Guid, string> names, Guid? id) => id is { } v && names.TryGetValue(v, out var n) ? n : null;
}
