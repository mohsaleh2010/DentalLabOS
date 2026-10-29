using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>
/// Delivery (plan 5.9, ORD-21..24): ready orders, delivery notes with a packaging check per line, courier runs and proof of
/// delivery. An order can be on one active note at a time; a failed note sends its orders back to the ready list.
/// </summary>
public sealed class DeliveryService(
    IApplicationDbContext db,
    IPermissionService permissions,
    ICurrentUser currentUser,
    INumberingService numbering,
    IClock clock,
    LabCalendar calendar,
    WorkOrderService workOrders)
{
    public const string DocumentType = "DeliveryNote";
    private const int MaxLines = 100;
    private const int MaxStops = 60;

    private Guid Me => currentUser.UserId ?? throw new ForbiddenException(Permissions.Delivery.Read);

    // ---- Ready for delivery (ORD-21) ----

    public async Task<IReadOnlyList<ReadyGroupDto>> ReadyAsync(string? search = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var today = await calendar.TodayAsync(ct);
        var query = db.WorkOrders.AsNoTracking().Where(w => w.Status == WorkOrderStatus.ReadyForDelivery && w.DeliveryNoteId == null);
        if (MasterData.Clean(search) is { } s)
        {
            query = query.Where(w => w.Number.Contains(s) || db.Customers.Any(c => c.Id == w.CustomerId && c.Name.Contains(s)));
        }

        var rows = await workOrders.Project(query.OrderBy(w => w.DueDate).ThenBy(w => w.Number).Take(500), today).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var customerOf = await db.WorkOrders.AsNoTracking().Where(w => ids.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.CustomerId, ct);
        var customerIds = customerOf.Values.Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Address, c.City, Phone = c.Mobile ?? c.Phone }).ToDictionaryAsync(c => c.Id, ct);
        return rows.GroupBy(r => customerOf[r.Id]).Select(g =>
            {
                var c = customers[g.Key];
                return new ReadyGroupDto(c.Id, c.Name, JoinAddress(c.Address, c.City), c.Phone, g.ToList());
            })
            .OrderBy(g => g.WorkOrders.Min(w => w.DueDate)).ThenBy(g => g.CustomerName, StringComparer.CurrentCulture).ToList();
    }

    private static string? JoinAddress(string? address, string? city) =>
        string.Join("، ", new[] { address, city }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } a ? a : null;

    // ---- Delivery notes (ORD-22/24) ----

    public async Task<PagedResult<DeliveryNoteListItemDto>> ListNotesAsync(PageQuery page, string? status = null, Guid? runId = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var query = db.DeliveryNotes.AsNoTracking();
        if (MasterData.TryParseEnum<DeliveryStatus>(status, out var st))
        {
            query = query.Where(n => n.Status == st);
        }
        else if (status == "active")
        {
            query = query.Where(n => n.Status == DeliveryStatus.Draft || n.Status == DeliveryStatus.OutForDelivery);
        }

        if (runId is { } r)
        {
            query = query.Where(n => n.RunId == r);
        }

        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(n => n.Number.Contains(s) || db.Customers.Any(c => c.Id == n.CustomerId && c.Name.Contains(s)));
        }

        return await NoteRows(query.OrderByDescending(n => n.DocumentDate).ThenByDescending(n => n.Number)).ToPagedAsync(page, ct);
    }

    private IQueryable<DeliveryNoteListItemDto> NoteRows(IQueryable<DeliveryNote> source) =>
        source.Select(n => new DeliveryNoteListItemDto(
            n.Id, n.Number, n.DocumentDate, n.CustomerId, db.Customers.Where(c => c.Id == n.CustomerId).Select(c => c.Name).First(), n.Status.ToString(),
            n.Lines.Count, n.Lines.Count(l => l.PackagingChecked), n.RunId,
            db.DeliveryRuns.Where(r => r.Id == n.RunId)
                .Select(r => r.CourierName ?? db.UserProfiles.Where(p => p.UserId == r.CourierUserId).Select(p => p.FullName).FirstOrDefault()).FirstOrDefault(),
            n.RunSequence, n.DeliveredAtUtc, n.ReceivedByName));

    public async Task<DeliveryNoteDto> GetNoteAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var n = await db.DeliveryNotes.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryNote));
        var woIds = n.Lines.Select(l => l.WorkOrderId).ToList();
        var wos = await db.WorkOrders.AsNoTracking().Where(w => woIds.Contains(w.Id))
            .Select(w => new
            {
                w.Id, w.Number, w.PatientReference, w.Quantity, w.Status, Teeth = w.Teeth.Select(t => t.ToothCode).ToList(),
                Service = db.Items.Where(i => i.Id == w.ServiceItemId).Select(i => i.Name).First(),
            })
            .ToDictionaryAsync(w => w.Id, ct);
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == n.CustomerId).Select(c => c.Name).FirstAsync(ct);
        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == n.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var run = n.RunId is { } rid ? await db.DeliveryRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct) : null;
        var names = await workOrders.UserNamesAsync([n.DeliveredByUserId, run?.CourierUserId], ct);
        var failure = n.FailureReasonCodeId is { } fid ? await db.ReasonCodes.AsNoTracking().Where(r => r.Id == fid).Select(r => r.Name).FirstOrDefaultAsync(ct) : null;
        return new DeliveryNoteDto(
            n.Id, n.Number, n.DocumentDate, n.Status.ToString(), n.BranchId, branch ?? string.Empty, n.CustomerId, customer, n.Address, n.ContactPhone, n.Notes,
            n.RunId, run is null ? null : RunLabel(run, names), n.RunSequence, n.DispatchedAtUtc, n.DeliveredAtUtc, n.ReceivedByName,
            n.DeliveredByUserId is { } u ? names.GetValueOrDefault(u) : null, failure, n.FailureNotes, n.ProofFileId is not null,
            n.Lines.OrderBy(l => wos.TryGetValue(l.WorkOrderId, out var w) ? w.Number : string.Empty, StringComparer.Ordinal).Select(l =>
            {
                var w = wos[l.WorkOrderId];
                return new DeliveryNoteLineDto(l.Id, w.Id, w.Number, w.Service, w.PatientReference, w.Quantity, w.Teeth.OrderBy(Fdi.Order).ToList(),
                    l.PackagingChecked, w.Status.ToString());
            }).ToList(),
            RowVersions.Encode(n.RowVersion));
    }

    public async Task<DeliveryNoteDto> CreateNoteAsync(DeliveryNoteCreateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var previous = await db.DeliveryNotes.AsNoTracking().Where(n => n.ClientOperationId == request.ClientOperationId && request.ClientOperationId != Guid.Empty)
            .Select(n => (Guid?)n.Id).FirstOrDefaultAsync(ct);
        if (previous is { } done)
        {
            return await GetNoteAsync(done, ct);
        }

        var ids = (request.WorkOrderIds ?? []).Distinct().ToList();
        var v = new Validator().When(request.ClientOperationId == Guid.Empty, nameof(request.ClientOperationId), "Validation.Required")
            .When(ids.Count == 0, nameof(request.WorkOrderIds), "Validation.SelectWorkOrders")
            .When(ids.Count > MaxLines, nameof(request.WorkOrderIds), "Validation.TooMany")
            .MaxLength(nameof(request.Notes), request.Notes, 1000);
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        v.When(customer is null, nameof(request.CustomerId), "Validation.Required");
        if (request.RunId is { } runId)
        {
            v.When(!await db.DeliveryRuns.AnyAsync(r => r.Id == runId && r.Status != DeliveryRunStatus.Completed, ct), nameof(request.RunId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        var orders = await db.WorkOrders.Where(w => ids.Contains(w.Id)).ToListAsync(ct);
        if (orders.Count != ids.Count || orders.Any(w => w.CustomerId != request.CustomerId))
        {
            throw ValidationException.For(nameof(request.WorkOrderIds), "Validation.DeliveryCustomer");
        }

        if (orders.Any(w => w.Status != WorkOrderStatus.ReadyForDelivery || w.DeliveryNoteId is not null))
        {
            throw new DomainException("Errors.Delivery.NotReady");
        }

        var today = await calendar.TodayAsync(ct);
        var noteId = await db.InTransactionAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            var branchId = orders.GroupBy(o => o.BranchId).OrderByDescending(g => g.Count()).First().Key;
            var note = new DeliveryNote
            {
                Number = await numbering.NextAsync(DocumentType, branchId, today, innerCt), BranchId = branchId, DocumentDate = today,
                ClientOperationId = request.ClientOperationId, CustomerId = customer!.Id, Address = JoinAddress(customer.Address, customer.City),
                ContactPhone = customer.Mobile ?? customer.Phone, Notes = MasterData.Clean(request.Notes),
            };
            if (request.RunId is { } run)
            {
                note.RunId = run;
                note.RunSequence = await db.DeliveryNotes.Where(n => n.RunId == run).MaxAsync(n => (int?)n.RunSequence, innerCt) + 1 ?? 1;
            }

            foreach (var w in orders)
            {
                note.Lines.Add(new DeliveryNoteLine { DeliveryNoteId = note.Id, WorkOrderId = w.Id });
                w.DeliveryNoteId = note.Id;
                db.StageTransitions.Add(Info(w, TransitionAction.AddedToDelivery, note.Number, now));
            }

            db.DeliveryNotes.Add(note);
            await db.SaveChangesAsync(innerCt);
            return note.Id;
        }, ct);
        return await GetNoteAsync(noteId, ct);
    }

    /// <summary>Packing check per line (plan 5.9: "فحص التغليف والملصق"). Only while the note is a draft.</summary>
    public async Task CheckPackagingAsync(Guid noteId, PackagingCheckRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var note = await LoadNoteAsync(noteId, ct);
        EnsureDraft(note);
        var ids = (request.LineIds ?? []).ToHashSet();
        foreach (var l in note.Lines.Where(l => ids.Contains(l.Id)))
        {
            l.PackagingChecked = request.Checked;
            l.CheckedByUserId = request.Checked ? Me : null;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveLineAsync(Guid noteId, Guid lineId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var note = await LoadNoteAsync(noteId, ct);
        EnsureDraft(note);
        var line = note.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new NotFoundException(nameof(DeliveryNoteLine));
        var now = clock.UtcNow;
        var wo = await db.WorkOrders.FirstAsync(w => w.Id == line.WorkOrderId, ct);
        wo.DeliveryNoteId = null;
        db.StageTransitions.Add(Info(wo, TransitionAction.RemovedFromDelivery, note.Number, now));
        note.Lines.Remove(line);
        db.DeliveryNoteLines.Remove(line);
        if (note.Lines.Count == 0)
        {
            note.Status = DeliveryStatus.Cancelled;
            note.CancelledAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task CancelNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var note = await LoadNoteAsync(noteId, ct);
        EnsureDraft(note);
        var now = clock.UtcNow;
        await ReleaseOrdersAsync(note, TransitionAction.RemovedFromDelivery, note.Number, now, ct);
        note.Status = DeliveryStatus.Cancelled;
        note.CancelledAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The note leaves the lab. Every line's packaging must be checked first.</summary>
    public async Task DispatchAsync(Guid noteId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var note = await LoadNoteAsync(noteId, ct);
        await DispatchCoreAsync(note, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task DispatchCoreAsync(DeliveryNote note, DateTime now, CancellationToken ct)
    {
        EnsureDraft(note);
        if (note.Lines.Any(l => !l.PackagingChecked))
        {
            throw new DomainException("Errors.Delivery.PackagingNotChecked");
        }

        note.Status = DeliveryStatus.OutForDelivery;
        note.DispatchedAtUtc = now;
        foreach (var wo in await OrdersOfAsync(note, ct))
        {
            db.StageTransitions.Add(Info(wo, TransitionAction.OutForDelivery, note.Number, now));
        }
    }

    /// <summary>ORD-24: received (with the receiver's name) or failed (with a reason). A handover at the counter may confirm a checked draft directly.</summary>
    public async Task ConfirmAsync(Guid noteId, DeliveryConfirmRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Confirm, ct);
        var note = await LoadNoteAsync(noteId, ct);
        if (!note.IsActive)
        {
            throw new DomainException("Errors.Delivery.Closed");
        }

        var now = clock.UtcNow;
        var v = new Validator().MaxLength(nameof(request.ReceivedByName), request.ReceivedByName, 150).MaxLength(nameof(request.FailureNotes), request.FailureNotes, 500);
        ReasonCode? failure = null;
        var at = request.DeliveredAtUtc ?? now;
        if (request.Delivered)
        {
            v.Required(nameof(request.ReceivedByName), request.ReceivedByName)
                .When(at > now.AddMinutes(5) || at < note.CreatedAtUtc.AddMinutes(-5), nameof(request.DeliveredAtUtc), "Validation.DeliveredAt");
        }
        else
        {
            failure = request.FailureReasonCodeId is { } fid
                ? await db.ReasonCodes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == fid && r.Scope == ReasonScope.DeliveryFailure && r.IsActive, ct)
                : null;
            v.When(failure is null, nameof(request.FailureReasonCodeId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        if (request.RowVersion is not null)
        {
            db.SetOriginalRowVersion(note, request.RowVersion);
        }

        if (request.Delivered)
        {
            if (note.Status == DeliveryStatus.Draft)
            {
                await DispatchCoreAsync(note, at, ct);
            }

            note.Status = DeliveryStatus.Delivered;
            note.DeliveredAtUtc = at;
            note.ReceivedByName = request.ReceivedByName!.Trim();
            note.DeliveredByUserId = Me;
            foreach (var wo in await OrdersOfAsync(note, ct))
            {
                var log = WorkflowEngine.MarkDelivered(wo, Me, now, $"{note.Number} — {note.ReceivedByName}");
                wo.DeliveredAtUtc = at;
                log.AtUtc = at;
                db.StageTransitions.Add(log);

                // A post-delivery adjustment is done when its order reaches the clinic.
                foreach (var a in await db.Adjustments.Where(a => a.AdjustmentWorkOrderId == wo.Id && a.Status == AdjustmentStatus.Open).ToListAsync(ct))
                {
                    a.Status = AdjustmentStatus.Closed;
                    a.ClosedAtUtc = now;
                }
            }
        }
        else
        {
            var text = MasterData.Clean(request.FailureNotes) is { } n ? $"{failure!.Name} — {n}" : failure!.Name;
            await ReleaseOrdersAsync(note, TransitionAction.DeliveryFailed, $"{note.Number} — {text}", now, ct);
            note.Status = DeliveryStatus.Failed;
            note.FailureReasonCodeId = failure.Id;
            note.FailureNotes = MasterData.Clean(request.FailureNotes);
            note.DeliveredByUserId = Me;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>A photo of the signed note or the parcel at the door (images or PDF only).</summary>
    public async Task UploadProofAsync(Guid noteId, Stream content, string fileName, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Confirm, ct);
        var note = await LoadNoteAsync(noteId, ct);
        if (note.Status is DeliveryStatus.Cancelled)
        {
            throw new DomainException("Errors.Delivery.Closed");
        }

        var (file, _) = await workOrders.StoreFileAsync(content, fileName, ct, "photo", "pdf");
        try
        {
            db.Files.Add(file);
            note.ProofFileId = file.Id;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await workOrders.DeleteStoredAsync(file.StorageKey);
            throw;
        }
    }

    public async Task<FileDownload> DownloadProofAsync(Guid noteId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var fileId = await db.DeliveryNotes.AsNoTracking().Where(n => n.Id == noteId).Select(n => n.ProofFileId).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException(nameof(FileMetadata));
        return await workOrders.OpenFileAsync(fileId, ct);
    }

    private async Task ReleaseOrdersAsync(DeliveryNote note, TransitionAction action, string reason, DateTime now, CancellationToken ct)
    {
        foreach (var wo in await OrdersOfAsync(note, ct))
        {
            if (wo.DeliveryNoteId == note.Id)
            {
                wo.DeliveryNoteId = null;
            }

            db.StageTransitions.Add(Info(wo, action, reason, now));
        }
    }

    private async Task<List<WorkOrder>> OrdersOfAsync(DeliveryNote note, CancellationToken ct)
    {
        var ids = note.Lines.Select(l => l.WorkOrderId).ToList();
        return await db.WorkOrders.Where(w => ids.Contains(w.Id)).ToListAsync(ct);
    }

    private async Task<DeliveryNote> LoadNoteAsync(Guid id, CancellationToken ct) =>
        await db.DeliveryNotes.Include(n => n.Lines).FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryNote));

    private static void EnsureDraft(DeliveryNote note)
    {
        if (note.Status != DeliveryStatus.Draft)
        {
            throw new DomainException("Errors.Delivery.NotDraft");
        }
    }

    private StageTransitionLog Info(WorkOrder wo, TransitionAction action, string reason, DateTime now) => new()
    {
        WorkOrderId = wo.Id, Action = action, FromStatus = wo.Status, ToStatus = wo.Status, UserId = Me, AtUtc = now, Reason = reason,
    };

    // ---- Runs (ORD-23) ----

    public async Task<IReadOnlyList<DeliveryRunListItemDto>> ListRunsAsync(DateOnly? from = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var since = from ?? (await calendar.TodayAsync(ct)).AddDays(-14);
        var runs = await db.DeliveryRuns.AsNoTracking().Where(r => r.RunDate >= since || r.Status != DeliveryRunStatus.Completed)
            .OrderByDescending(r => r.RunDate).ThenBy(r => r.CreatedAtUtc).Take(200)
            .Select(r => new
            {
                Run = r,
                Stops = db.DeliveryNotes.Count(n => n.RunId == r.Id && n.Status != DeliveryStatus.Cancelled),
                Delivered = db.DeliveryNotes.Count(n => n.RunId == r.Id && n.Status == DeliveryStatus.Delivered),
                Failed = db.DeliveryNotes.Count(n => n.RunId == r.Id && n.Status == DeliveryStatus.Failed),
            })
            .ToListAsync(ct);
        var names = await workOrders.UserNamesAsync(runs.Select(r => r.Run.CourierUserId), ct);
        return runs.Select(r => new DeliveryRunListItemDto(r.Run.Id, r.Run.RunDate, Courier(r.Run, names), r.Run.Status.ToString(), r.Stops, r.Delivered, r.Failed,
            r.Run.StartedAtUtc, r.Run.CompletedAtUtc)).ToList();
    }

    public async Task<DeliveryRunDto> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Read, ct);
        var run = await db.DeliveryRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryRun));
        var stops = await NoteRows(db.DeliveryNotes.AsNoTracking().Where(n => n.RunId == id && n.Status != DeliveryStatus.Cancelled).OrderBy(n => n.RunSequence))
            .ToListAsync(ct);
        var names = await workOrders.UserNamesAsync([run.CourierUserId], ct);
        return new DeliveryRunDto(run.Id, run.RunDate, run.CourierUserId, Courier(run, names), run.Status.ToString(), run.Notes, run.StartedAtUtc, run.CompletedAtUtc,
            stops, RowVersions.Encode(run.RowVersion));
    }

    public async Task<Guid> SaveRunAsync(Guid? id, DeliveryRunRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var today = await calendar.TodayAsync(ct);
        var v = new Validator().MaxLength(nameof(request.CourierName), request.CourierName, 150).MaxLength(nameof(request.Notes), request.Notes, 500)
            .When(request.CourierUserId is null && string.IsNullOrWhiteSpace(request.CourierName), nameof(request.CourierUserId), "Validation.Courier")
            .When(request.RunDate < today.AddDays(-30) || request.RunDate > today.AddDays(60), nameof(request.RunDate), "Validation.DueDate");
        if (request.CourierUserId is { } uid)
        {
            v.When(!await db.UserProfiles.AnyAsync(p => p.UserId == uid && p.IsActive, ct), nameof(request.CourierUserId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        var run = id is { } existing
            ? await db.DeliveryRuns.FirstOrDefaultAsync(r => r.Id == existing, ct) ?? throw new NotFoundException(nameof(DeliveryRun))
            : db.DeliveryRuns.Add(new DeliveryRun()).Entity;
        if (id is not null)
        {
            if (run.Status == DeliveryRunStatus.Completed)
            {
                throw new DomainException("Errors.Delivery.RunCompleted");
            }

            db.SetOriginalRowVersion(run, request.RowVersion);
        }

        run.RunDate = request.RunDate;
        run.CourierUserId = request.CourierUserId;
        run.CourierName = request.CourierUserId is null ? MasterData.Clean(request.CourierName) : null;
        run.Notes = MasterData.Clean(request.Notes);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }

    /// <summary>Sets the run's stops in visiting order. Notes left out come off the run; only open notes can be on it.</summary>
    public async Task SetStopsAsync(Guid runId, RunStopsRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var run = await db.DeliveryRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException(nameof(DeliveryRun));
        if (run.Status == DeliveryRunStatus.Completed)
        {
            throw new DomainException("Errors.Delivery.RunCompleted");
        }

        var ids = (request.NoteIds ?? []).Distinct().ToList();
        if (ids.Count > MaxStops)
        {
            throw ValidationException.For(nameof(request.NoteIds), "Validation.TooMany");
        }

        var notes = await db.DeliveryNotes.Where(n => ids.Contains(n.Id) || n.RunId == runId).ToListAsync(ct);
        // New stops must be drafts; notes already on this run keep their place whatever their state.
        if (notes.Count(n => ids.Contains(n.Id)) != ids.Count || notes.Any(n => ids.Contains(n.Id) && n.RunId != runId && n.Status != DeliveryStatus.Draft))
        {
            throw new DomainException("Errors.Delivery.NotDraft");
        }

        foreach (var n in notes.Where(n => n.RunId == runId && !ids.Contains(n.Id) && n.IsActive))
        {
            n.RunId = null;
            n.RunSequence = 0;
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var n = notes.First(x => x.Id == ids[i]);
            n.RunId = runId;
            n.RunSequence = i + 1;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The courier leaves: every draft note on the run is dispatched (their packaging must be checked).</summary>
    public async Task StartRunAsync(Guid runId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var run = await db.DeliveryRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException(nameof(DeliveryRun));
        if (run.Status != DeliveryRunStatus.Planned)
        {
            throw new DomainException("Errors.Delivery.RunState");
        }

        var notes = await db.DeliveryNotes.Include(n => n.Lines).Where(n => n.RunId == runId && n.Status == DeliveryStatus.Draft).ToListAsync(ct);
        if (notes.Count == 0 && !await db.DeliveryNotes.AnyAsync(n => n.RunId == runId && n.Status == DeliveryStatus.OutForDelivery, ct))
        {
            throw new DomainException("Errors.Delivery.RunEmpty");
        }

        var now = clock.UtcNow;
        foreach (var n in notes)
        {
            await DispatchCoreAsync(n, now, ct);
        }

        run.Status = DeliveryRunStatus.OnRoute;
        run.StartedAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteRunAsync(Guid runId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        var run = await db.DeliveryRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException(nameof(DeliveryRun));
        if (run.Status != DeliveryRunStatus.OnRoute)
        {
            throw new DomainException("Errors.Delivery.RunState");
        }

        if (await db.DeliveryNotes.AnyAsync(n => n.RunId == runId && (n.Status == DeliveryStatus.Draft || n.Status == DeliveryStatus.OutForDelivery), ct))
        {
            throw new DomainException("Errors.Delivery.RunOpenStops");
        }

        run.Status = DeliveryRunStatus.Completed;
        run.CompletedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lab users who can be picked as the courier.</summary>
    public async Task<IReadOnlyList<OptionDto>> CourierOptionsAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        return await db.UserProfiles.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.FullName)
            .Select(p => new OptionDto(p.UserId, string.Empty, p.FullName)).ToListAsync(ct);
    }

    /// <summary>Open notes not on a run yet, for adding stops.</summary>
    public async Task<IReadOnlyList<DeliveryNoteListItemDto>> UnplannedNotesAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Delivery.Manage, ct);
        return await NoteRows(db.DeliveryNotes.AsNoTracking().Where(n => n.RunId == null && n.Status == DeliveryStatus.Draft).OrderBy(n => n.Number)).ToListAsync(ct);
    }

    private static string Courier(DeliveryRun r, IReadOnlyDictionary<Guid, string> names) =>
        r.CourierUserId is { } u && names.TryGetValue(u, out var n) ? n : r.CourierName ?? "—";

    private static string RunLabel(DeliveryRun r, IReadOnlyDictionary<Guid, string> names) => $"{r.RunDate:yyyy-MM-dd} · {Courier(r, names)}";
}
