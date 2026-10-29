using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>
/// Quality (plan 5.6): checklist templates (ADM-17), the inspection queue and decision (ORD-17/18), adjustments (ORD-19)
/// and remakes (ORD-20). Every decision names the inspector and the time; nothing is assumed about who is at fault.
/// </summary>
public sealed class QualityService(
    IApplicationDbContext db,
    IPermissionService permissions,
    ICurrentUser currentUser,
    IClock clock,
    LabCalendar calendar,
    WorkOrderService workOrders)
{
    private const int MaxRows = 500;
    private const int MaxTemplateItems = 60;

    private Guid Me => currentUser.UserId ?? throw new ForbiddenException(Permissions.Quality.Read);

    // ---- Templates (ADM-17) ----

    public async Task<IReadOnlyList<QualityTemplateListItemDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        await DemandTemplatesReadAsync(ct);
        return await db.QualityTemplates.AsNoTracking().OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.IsActive).ThenBy(t => t.Name)
            .Select(t => new QualityTemplateListItemDto(t.Id, t.Name, t.Description, t.IsActive, t.IsDefault, t.Items.Count, t.Items.Count(i => i.IsCritical),
                db.WorkflowStages.Count(s => s.QualityTemplateId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<QualityTemplateDto> GetTemplateAsync(Guid id, CancellationToken ct = default)
    {
        await DemandTemplatesReadAsync(ct);
        var t = await db.QualityTemplates.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException(nameof(QualityTemplate));
        return Map(t);
    }

    public async Task<QualityTemplateDto> SaveTemplateAsync(Guid? id, QualityTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.QualityManage, ct);
        var name = request.Name?.Trim();
        var items = (request.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150)
            .MaxLength(nameof(request.Description), request.Description, 500)
            .When(items.Count == 0, nameof(request.Items), "Validation.ChecklistItems")
            .When(items.Count > MaxTemplateItems, nameof(request.Items), "Validation.TooMany")
            .When(request.IsDefault && !request.IsActive, nameof(request.IsDefault), "Validation.DefaultInactive");
        for (var i = 0; i < items.Count; i++)
        {
            v.MaxLength($"Items[{i}].Text", items[i].Text, 300);
        }

        v.When(name is not null && await db.QualityTemplates.AnyAsync(t => t.Name == name && t.Id != id, ct), nameof(request.Name), "Validation.NameTaken");
        QualityTemplate? template = null;
        if (id is { } existing)
        {
            template = await db.QualityTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == existing, ct)
                       ?? throw new NotFoundException(nameof(QualityTemplate));
            v.When(!request.IsActive && await db.WorkflowStages.AnyAsync(s => s.QualityTemplateId == existing, ct), nameof(request.IsActive), "Validation.TemplateInUse");
        }

        v.ThrowIfInvalid();
        return await db.InTransactionAsync(async innerCt =>
        {
            if (request.IsDefault && template?.IsDefault != true)
            {
                // A filtered unique index allows one default: clear the old one first.
                var old = await db.QualityTemplates.Where(t => t.IsDefault).ToListAsync(innerCt);
                old.ForEach(t => t.IsDefault = false);
                await db.SaveChangesAsync(innerCt);
            }

            if (template is null)
            {
                template = new QualityTemplate();
                db.QualityTemplates.Add(template);
            }
            else
            {
                db.SetOriginalRowVersion(template, request.RowVersion);
            }

            template.Name = name!;
            template.Description = MasterData.Clean(request.Description);
            template.IsActive = request.IsActive;
            template.IsDefault = request.IsDefault;

            // Past checks copied their item texts, so rows can be replaced freely.
            var keep = new HashSet<Guid>();
            for (var i = 0; i < items.Count; i++)
            {
                var r = items[i];
                var row = r.Id is { } rid ? template.Items.FirstOrDefault(x => x.Id == rid) : null;
                if (row is null)
                {
                    row = new QualityTemplateItem { QualityTemplateId = template.Id };
                    template.Items.Add(row);
                    db.QualityTemplateItems.Add(row);
                }

                keep.Add(row.Id);
                row.Order = i + 1;
                row.Text = r.Text.Trim();
                row.IsCritical = r.IsCritical;
            }

            foreach (var gone in template.Items.Where(x => !keep.Contains(x.Id)).ToList())
            {
                template.Items.Remove(gone);
                db.QualityTemplateItems.Remove(gone);
            }

            await db.SaveChangesAsync(innerCt);
            return Map(template);
        }, ct);
    }

    /// <summary>Active templates for the route designer.</summary>
    public async Task<IReadOnlyList<Contracts.Catalog.OptionDto>> TemplateOptionsAsync(CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Admin.WorkflowsManage, ct))
        {
            await DemandTemplatesReadAsync(ct);
        }

        return await db.QualityTemplates.AsNoTracking().Where(t => t.IsActive).OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new Contracts.Catalog.OptionDto(t.Id, t.IsDefault ? "*" : string.Empty, t.Name)).ToListAsync(ct);
    }

    private async Task DemandTemplatesReadAsync(CancellationToken ct)
    {
        if (!await permissions.HasAsync(Permissions.Admin.QualityManage, ct))
        {
            await permissions.DemandAsync(Permissions.Quality.Read, ct);
        }
    }

    private static QualityTemplateDto Map(QualityTemplate t) =>
        new(t.Id, t.Name, t.Description, t.IsActive, t.IsDefault,
            t.Items.OrderBy(i => i.Order).Select(i => new QualityTemplateItemDto(i.Id, i.Order, i.Text, i.IsCritical)).ToList(),
            RowVersions.Encode(t.RowVersion));

    // ---- Queue and inspection (ORD-17/18) ----

    public async Task<QualityOverviewDto> OverviewAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.Read, ct);
        var zone = await calendar.ZoneAsync(ct);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, zone);
        var dayStart = TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date, zone);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1), zone);
        var awaiting = await AwaitingStages().CountAsync(ct);
        var today = await db.QualityChecks.AsNoTracking().Where(c => c.CheckedAtUtc >= dayStart).GroupBy(c => c.Result)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return new QualityOverviewDto(awaiting, today.Sum(x => x.Count), today.Where(x => x.Key != QualityResult.Accepted).Sum(x => x.Count),
            await db.Adjustments.CountAsync(a => a.Status == AdjustmentStatus.Open, ct),
            await db.Remakes.CountAsync(r => r.DecidedAtUtc >= monthStart, ct));
    }

    public async Task<IReadOnlyList<QualityQueueItemDto>> QueueAsync(Guid? departmentId = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.Read, ct);
        var query = AwaitingStages();
        if (departmentId is { } d)
        {
            query = query.Where(s => s.DepartmentId == d);
        }

        var rows = await query.OrderBy(s => s.WorkOrder.DueDate).ThenByDescending(s => s.WorkOrder.Priority).ThenBy(s => s.WorkOrder.Number).Take(MaxRows)
            .Select(s => new
            {
                s.Id, s.WorkOrderId, s.WorkOrder.Number, s.WorkOrder.Kind, s.Name, s.DepartmentId, s.WorkOrder.Priority, s.WorkOrder.DueDate,
                Customer = db.Customers.Where(c => c.Id == s.WorkOrder.CustomerId).Select(c => c.Name).First(),
                Service = db.Items.Where(i => i.Id == s.WorkOrder.ServiceItemId).Select(i => i.Name).First(),
                Teeth = s.WorkOrder.Teeth.Count,
                Checks = db.QualityChecks.Count(c => c.WorkOrderId == s.WorkOrderId),
                Sent = db.StageTransitions.Where(l => l.ToStageId == s.Id && l.Action == TransitionAction.SentToQuality).OrderByDescending(l => l.AtUtc)
                    .Select(l => new { l.AtUtc, l.UserId }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var today = await calendar.TodayAsync(ct);
        var names = await workOrders.UserNamesAsync(rows.Select(r => r.Sent?.UserId), ct);
        var depts = await db.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return rows.Select(r => new QualityQueueItemDto(r.Id, r.WorkOrderId, r.Number, r.Kind.ToString(), r.Customer, r.Service, r.Name,
                r.DepartmentId is { } dd ? depts.GetValueOrDefault(dd) : null, r.Priority.ToString(), r.DueDate, r.DueDate < today, r.Sent?.AtUtc,
                r.Sent is { } sent ? names.GetValueOrDefault(sent.UserId) : null, r.Teeth, r.Checks))
            .ToList();
    }

    private IQueryable<WorkOrderStage> AwaitingStages() =>
        db.WorkOrderStages.AsNoTracking().Where(s => s.Status == StageStatus.AwaitingQuality
                                                     && (s.WorkOrder.Status == WorkOrderStatus.InQuality || s.WorkOrder.Status == WorkOrderStatus.InProduction));

    /// <summary>The inspection screen for a work order. <paramref name="stageId"/> picks one stage when several wait in parallel.</summary>
    public async Task<QualityInspectionDto> InspectionAsync(Guid workOrderId, Guid? stageId = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.Read, ct);
        var wo = await workOrders.GetAsync(workOrderId, ct);
        var stage = wo.Stages.Where(s => s.Status == nameof(StageStatus.AwaitingQuality)).FirstOrDefault(s => stageId is null || s.Id == stageId)
                    ?? throw new DomainException("Errors.Quality.NotAwaiting");
        var stageRow = await db.WorkOrderStages.AsNoTracking().Where(s => s.Id == stage.Id).Select(s => new { s.QualityTemplateId }).FirstAsync(ct);
        var template = await ResolveTemplateAsync(stageRow.QualityTemplateId, ct);
        return new QualityInspectionDto(wo, stage.Id, stage.Name, template?.Id, template?.Name,
            template?.Items.OrderBy(i => i.Order).Select(i => new QualityTemplateItemDto(i.Id, i.Order, i.Text, i.IsCritical)).ToList() ?? [],
            wo.Stages.Where(s => s.Step <= stage.Step).ToList(), await HistoryAsync(workOrderId, ct));
    }

    /// <summary>The stage's own template, else the lab's default; null means a decision without a checklist.</summary>
    private async Task<QualityTemplate?> ResolveTemplateAsync(Guid? templateId, CancellationToken ct)
    {
        var query = db.QualityTemplates.AsNoTracking().Include(t => t.Items);
        return (templateId is { } id ? await query.FirstOrDefaultAsync(t => t.Id == id, ct) : null)
               ?? await query.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive, ct);
    }

    public async Task<QualityCheckResultDto> InspectAsync(Guid workOrderId, QualityCheckRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.Inspect, ct);
        var v = new Validator().MaxLength(nameof(request.Notes), request.Notes, 2000)
            .When(!MasterData.TryParseEnum<QualityResult>(request.Result, out var result) || result == QualityResult.Pending, nameof(request.Result), "Validation.Required");
        var wo = await workOrders.LoadAsync(workOrderId, ct);
        var stage = wo.Stages.FirstOrDefault(s => s.Id == request.StageId) ?? throw new NotFoundException(nameof(WorkOrderStage));
        if (stage.Status != StageStatus.AwaitingQuality)
        {
            throw new DomainException("Errors.Quality.NotAwaiting");
        }

        var template = await ResolveTemplateAsync(stage.QualityTemplateId, ct);
        var answers = (request.Items ?? []).GroupBy(i => i.TemplateItemId).ToDictionary(g => g.Key, g => g.First());
        foreach (var a in answers.Values)
        {
            v.MaxLength("Items", a.Note, 500);
        }

        var checklist = template?.Items.OrderBy(i => i.Order).ToList() ?? [];
        v.When(checklist.Any(i => !answers.ContainsKey(i.Id)), nameof(request.Items), "Validation.ChecklistIncomplete");

        ReasonCode? reason = null;
        WorkOrderStage? returnTo = null;
        if (result is QualityResult.Rejected or QualityResult.AdjustmentRequested)
        {
            var scope = result == QualityResult.Rejected ? ReasonScope.Rejection : ReasonScope.Adjustment;
            reason = request.ReasonCodeId is { } rid ? await db.ReasonCodes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid && r.Scope == scope && r.IsActive, ct) : null;
            v.When(reason is null, nameof(request.ReasonCodeId), "Validation.Required");
            returnTo = wo.Stages.FirstOrDefault(s => s.Id == (request.ReturnToStageId ?? stage.Id));
            v.When(returnTo is null, nameof(request.ReturnToStageId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        var now = clock.UtcNow;
        var criticalFailed = checklist.Any(i => i.IsCritical && !answers[i.Id].Passed);
        var reasonText = reason is null ? MasterData.Clean(request.Notes) : MasterData.Clean(request.Notes) is { } n ? $"{reason.Name} — {n}" : reason.Name;
        db.StageTransitions.Add(WorkflowEngine.Inspect(wo, stage, result, criticalFailed, returnTo, Me, now, reasonText));

        var check = new QualityCheck
        {
            WorkOrderId = wo.Id, StageId = stage.Id, StageName = stage.Name, TemplateId = template?.Id, TemplateName = template?.Name, InspectorUserId = Me,
            Result = result, ReasonCodeId = reason?.Id, Category = reason?.Category, ReturnToStageId = returnTo?.Id, Notes = MasterData.Clean(request.Notes),
            CheckedAtUtc = now,
        };
        foreach (var i in checklist)
        {
            var a = answers[i.Id];
            check.Items.Add(new QualityCheckItem
            {
                QualityCheckId = check.Id, TemplateItemId = i.Id, Order = i.Order, Text = i.Text, IsCritical = i.IsCritical, Passed = a.Passed, Note = MasterData.Clean(a.Note),
            });
        }

        db.QualityChecks.Add(check);
        CaseAdjustment? adjustment = null;
        if (result == QualityResult.AdjustmentRequested)
        {
            adjustment = new CaseAdjustment
            {
                WorkOrderId = wo.Id, Source = AdjustmentSource.Quality, QualityCheckId = check.Id, ReasonCodeId = reason!.Id, Category = reason.Category,
                Notes = MasterData.Clean(request.Notes), RequestedByUserId = Me, RequestedAtUtc = now,
            };
            db.Adjustments.Add(adjustment);
        }

        if (result == QualityResult.Accepted)
        {
            await CloseAdjustmentsAsync(wo.Id, AdjustmentSource.Quality, now, ct);
        }

        await db.SaveChangesAsync(ct);
        return new QualityCheckResultDto(check.Id, wo.Status.ToString(), adjustment?.Id);
    }

    /// <summary>Quality's own adjustment requests close when the corrected work passes inspection.</summary>
    private async Task CloseAdjustmentsAsync(Guid workOrderId, AdjustmentSource source, DateTime now, CancellationToken ct)
    {
        foreach (var a in await db.Adjustments.Where(a => a.WorkOrderId == workOrderId && a.Source == source && a.Status == AdjustmentStatus.Open).ToListAsync(ct))
        {
            a.Status = AdjustmentStatus.Closed;
            a.ClosedAtUtc = now;
        }
    }

    public async Task<IReadOnlyList<QualityCheckDto>> HistoryAsync(Guid workOrderId, CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Quality.Read, ct))
        {
            await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        }

        return await ChecksAsync(db.QualityChecks.AsNoTracking().Where(c => c.WorkOrderId == workOrderId), ct);
    }

    /// <summary>Recent decisions across the lab (rejections and adjustment requests for the quality log).</summary>
    public async Task<IReadOnlyList<QualityCheckDto>> RecentChecksAsync(string? result = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.Read, ct);
        var query = db.QualityChecks.AsNoTracking();
        if (MasterData.TryParseEnum<QualityResult>(result, out var r))
        {
            query = query.Where(c => c.Result == r);
        }

        return await ChecksAsync(query, ct, 200);
    }

    private async Task<IReadOnlyList<QualityCheckDto>> ChecksAsync(IQueryable<QualityCheck> query, CancellationToken ct, int take = MaxRows)
    {
        var checks = await query.Include(c => c.Items).OrderByDescending(c => c.CheckedAtUtc).Take(take).ToListAsync(ct);
        var reasonIds = checks.Where(c => c.ReasonCodeId != null).Select(c => c.ReasonCodeId!.Value).Distinct().ToList();
        var reasons = await db.ReasonCodes.AsNoTracking().Where(r => reasonIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var stageIds = checks.Where(c => c.ReturnToStageId != null).Select(c => c.ReturnToStageId!.Value).Distinct().ToList();
        var stages = await db.WorkOrderStages.AsNoTracking().Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var woIds = checks.Select(c => c.WorkOrderId).Distinct().ToList();
        var numbers = await db.WorkOrders.AsNoTracking().Where(w => woIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Number, ct);
        var names = await workOrders.UserNamesAsync(checks.Select(c => (Guid?)c.InspectorUserId), ct);
        return checks.Select(c => new QualityCheckDto(c.Id, c.WorkOrderId, numbers.GetValueOrDefault(c.WorkOrderId) ?? string.Empty, c.StageName,
                c.Result.ToString(), c.ReasonCodeId is { } r ? reasons.GetValueOrDefault(r) : null, c.Category?.ToString(),
                c.ReturnToStageId is { } s ? stages.GetValueOrDefault(s) : null, c.Notes, c.CheckedAtUtc, names.GetValueOrDefault(c.InspectorUserId),
                c.Items.OrderBy(i => i.Order).Select(i => new QualityCheckItemDto(i.Text, i.IsCritical, i.Passed, i.Note)).ToList()))
            .ToList();
    }

    // ---- Adjustments (ORD-19) ----

    public async Task<IReadOnlyList<AdjustmentDto>> AdjustmentsAsync(bool openOnly = true, Guid? workOrderId = null, CancellationToken ct = default)
    {
        if (workOrderId is null || !await permissions.HasAsync(Permissions.WorkOrders.Read, ct))
        {
            await permissions.DemandAsync(Permissions.Quality.Read, ct);
        }

        var query = db.Adjustments.AsNoTracking();
        if (openOnly)
        {
            query = query.Where(a => a.Status == AdjustmentStatus.Open);
        }

        if (workOrderId is { } wid)
        {
            query = query.Where(a => a.WorkOrderId == wid || a.AdjustmentWorkOrderId == wid);
        }

        var rows = await query.OrderByDescending(a => a.RequestedAtUtc).Take(MaxRows)
            .Select(a => new
            {
                a.Id, a.WorkOrderId, a.Source, a.Status, a.Category, a.Notes, a.RequestedAtUtc, a.RequestedByUserId, a.ClosedAtUtc, a.AdjustmentWorkOrderId,
                Number = db.WorkOrders.Where(w => w.Id == a.WorkOrderId).Select(w => w.Number).First(),
                Customer = db.WorkOrders.Where(w => w.Id == a.WorkOrderId).SelectMany(w => db.Customers.Where(c => c.Id == w.CustomerId).Select(c => c.Name)).First(),
                Service = db.WorkOrders.Where(w => w.Id == a.WorkOrderId).SelectMany(w => db.Items.Where(i => i.Id == w.ServiceItemId).Select(i => i.Name)).First(),
                Reason = db.ReasonCodes.Where(r => r.Id == a.ReasonCodeId).Select(r => r.Name).First(),
                LinkedNumber = db.WorkOrders.Where(w => w.Id == a.AdjustmentWorkOrderId).Select(w => w.Number).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var names = await workOrders.UserNamesAsync(rows.Select(r => (Guid?)r.RequestedByUserId), ct);
        return rows.Select(r => new AdjustmentDto(r.Id, r.WorkOrderId, r.Number, r.Customer, r.Service, r.Source.ToString(), r.Status.ToString(), r.Reason,
            r.Category.ToString(), r.Notes, r.RequestedAtUtc, names.GetValueOrDefault(r.RequestedByUserId), r.ClosedAtUtc, r.AdjustmentWorkOrderId, r.LinkedNumber)).ToList();
    }

    /// <summary>The clinic returned a delivered case for correction: opens a linked adjustment order (kept apart from remakes).</summary>
    public async Task<LinkedWorkOrderResultDto> RequestCustomerAdjustmentAsync(Guid workOrderId, CustomerAdjustmentRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.RemakeManage, ct);
        if (await ExistingLinkedAsync(request.ClientOperationId, ct) is { } done)
        {
            var record = await db.Adjustments.AsNoTracking().Where(a => a.AdjustmentWorkOrderId == done.Id).Select(a => a.Id).FirstOrDefaultAsync(ct);
            return new LinkedWorkOrderResultDto(record, done.Id, done.Number);
        }

        var reason = await db.ReasonCodes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReasonCodeId && r.Scope == ReasonScope.Adjustment && r.IsActive, ct);
        var (priority, due) = await ValidateLinkedAsync(reason, request.ClientOperationId, request.Notes, request.DueDate, request.Priority, ct);
        var original = await workOrders.LoadAsync(workOrderId, ct);
        if (original.Status != WorkOrderStatus.Delivered)
        {
            throw new DomainException("Errors.Adjustment.NotDelivered");
        }

        return await db.InTransactionAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            var wo = await workOrders.CreateLinkedAsync(original, WorkOrderKind.Adjustment, request.ClientOperationId, due, priority, request.PriorityReason, false,
                reason!.Name, innerCt);
            var adjustment = new CaseAdjustment
            {
                WorkOrderId = original.Id, Source = AdjustmentSource.Customer, AdjustmentWorkOrderId = wo.Id, ReasonCodeId = reason.Id, Category = reason.Category,
                Notes = MasterData.Clean(request.Notes), RequestedByUserId = Me, RequestedAtUtc = now,
            };
            db.Adjustments.Add(adjustment);
            db.StageTransitions.Add(new StageTransitionLog
            {
                WorkOrderId = original.Id, Action = TransitionAction.AdjustmentRequested, FromStatus = original.Status, ToStatus = original.Status, UserId = Me,
                AtUtc = now, Reason = $"{wo.Number} — {reason.Name}",
            });
            await db.SaveChangesAsync(innerCt);
            return new LinkedWorkOrderResultDto(adjustment.Id, wo.Id, wo.Number);
        }, ct);
    }

    // ---- Remakes (ORD-20) ----

    public async Task<IReadOnlyList<RemakeListItemDto>> RemakesAsync(Guid? workOrderId = null, CancellationToken ct = default)
    {
        if (workOrderId is null || !await permissions.HasAsync(Permissions.WorkOrders.Read, ct))
        {
            await permissions.DemandAsync(Permissions.Quality.Read, ct);
        }

        var query = db.Remakes.AsNoTracking();
        if (workOrderId is { } wid)
        {
            query = query.Where(r => r.OriginalWorkOrderId == wid || r.RemakeWorkOrderId == wid);
        }

        var rows = await query.OrderByDescending(r => r.DecidedAtUtc).Take(MaxRows)
            .Select(r => new
            {
                r.Id, r.OriginalWorkOrderId, r.RemakeWorkOrderId, r.Category, r.ChargeTo, r.Decision, r.DecidedAtUtc, r.DecidedByUserId,
                Original = db.WorkOrders.Where(w => w.Id == r.OriginalWorkOrderId).Select(w => new { w.Number, w.CustomerId, w.ServiceItemId }).First(),
                Remake = db.WorkOrders.Where(w => w.Id == r.RemakeWorkOrderId).Select(w => new { w.Number, w.Status }).First(),
                Reason = db.ReasonCodes.Where(x => x.Id == r.ReasonCodeId).Select(x => x.Name).First(),
            })
            .ToListAsync(ct);
        var customerIds = rows.Select(r => r.Original.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var serviceIds = rows.Select(r => r.Original.ServiceItemId).Distinct().ToList();
        var services = await db.Items.AsNoTracking().Where(i => serviceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        var names = await workOrders.UserNamesAsync(rows.Select(r => (Guid?)r.DecidedByUserId), ct);
        return rows.Select(r => new RemakeListItemDto(r.Id, r.OriginalWorkOrderId, r.Original.Number, r.RemakeWorkOrderId, r.Remake.Number, r.Remake.Status.ToString(),
            customers.GetValueOrDefault(r.Original.CustomerId) ?? string.Empty, services.GetValueOrDefault(r.Original.ServiceItemId) ?? string.Empty, r.Reason,
            r.Category.ToString(), r.ChargeTo.ToString(), r.Decision, r.DecidedAtUtc, names.GetValueOrDefault(r.DecidedByUserId))).ToList();
    }

    public async Task<LinkedWorkOrderResultDto> CreateRemakeAsync(RemakeRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Quality.RemakeManage, ct);
        if (await ExistingLinkedAsync(request.ClientOperationId, ct) is { } done)
        {
            var record = await db.Remakes.AsNoTracking().Where(r => r.RemakeWorkOrderId == done.Id).Select(r => r.Id).FirstOrDefaultAsync(ct);
            return new LinkedWorkOrderResultDto(record, done.Id, done.Number);
        }

        var reason = await db.ReasonCodes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReasonCodeId && r.Scope == ReasonScope.Remake && r.IsActive, ct);
        var v = new Validator().MaxLength(nameof(request.Decision), request.Decision, 1000)
            .When(!MasterData.TryParseEnum<RemakeChargeTo>(request.ChargeTo, out var chargeTo), nameof(request.ChargeTo), "Validation.Required");
        v.ThrowIfInvalid();
        var (priority, due) = await ValidateLinkedAsync(reason, request.ClientOperationId, request.Decision, request.DueDate, request.Priority, ct);
        var original = await workOrders.LoadAsync(request.OriginalWorkOrderId, ct);

        return await db.InTransactionAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            foreach (var s in original.Stages)
            {
                WorkOrderService.CloseEntries(s, now);
            }

            var text = MasterData.Clean(request.Decision) is { } d ? $"{reason!.Name} — {d}" : reason!.Name;
            var wo = await workOrders.CreateLinkedAsync(original, WorkOrderKind.Remake, request.ClientOperationId, due, priority, request.PriorityReason,
                chargeTo == RemakeChargeTo.Customer, reason.Name, innerCt);
            var log = WorkflowEngine.ReplaceByRemake(original, Me, now, $"{wo.Number} — {text}");
            db.StageTransitions.Add(log);
            var remake = new RemakeRecord
            {
                OriginalWorkOrderId = original.Id, RemakeWorkOrderId = wo.Id, ReasonCodeId = reason.Id, Category = reason.Category, ChargeTo = chargeTo,
                Decision = MasterData.Clean(request.Decision), DecidedByUserId = Me, DecidedAtUtc = now,
                QualityCheckId = await db.QualityChecks.Where(c => c.WorkOrderId == original.Id).OrderByDescending(c => c.CheckedAtUtc).Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(innerCt),
            };
            db.Remakes.Add(remake);
            await CloseAdjustmentsAsync(original.Id, AdjustmentSource.Quality, now, innerCt);
            await db.SaveChangesAsync(innerCt);
            return new LinkedWorkOrderResultDto(remake.Id, wo.Id, wo.Number);
        }, ct);
    }

    private async Task<(PriorityLevel Priority, DateOnly? Due)> ValidateLinkedAsync(
        ReasonCode? reason, Guid clientOperationId, string? notes, DateOnly? due, string? priorityText, CancellationToken ct)
    {
        var today = await calendar.TodayAsync(ct);
        new Validator().When(reason is null, "ReasonCodeId", "Validation.Required")
            .When(clientOperationId == Guid.Empty, "ClientOperationId", "Validation.Required")
            .MaxLength("Notes", notes, 1000)
            .When(!MasterData.TryParseEnum<PriorityLevel>(priorityText, out var priority), "Priority", "Validation.Required")
            .When(due is { } d && (d < today || d > today.AddYears(1)), "DueDate", "Validation.DueDate")
            .ThrowIfInvalid();
        return (priority, due);
    }

    /// <summary>A retried submit returns the order the first one opened.</summary>
    private async Task<(Guid Id, string Number)?> ExistingLinkedAsync(Guid clientOperationId, CancellationToken ct)
    {
        if (clientOperationId == Guid.Empty)
        {
            return null;
        }

        var wo = await db.WorkOrders.AsNoTracking().Where(w => w.ClientOperationId == clientOperationId).Select(w => new { w.Id, w.Number }).FirstOrDefaultAsync(ct);
        return wo is null ? null : (wo.Id, wo.Number);
    }
}
