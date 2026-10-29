using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Production;

/// <summary>A batch of work received from one clinic (plan A2.3). It may hold a single work order.</summary>
public class CustomerOrder : DocumentEntity
{
    public Guid CustomerId { get; set; }
    public Guid? DentistId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateOnly? RequestedDeliveryDate { get; set; }
    public PriorityLevel Priority { get; set; }
    public string? DeliveryInstructions { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

/// <summary>The unit of production and costing, created from a service item.</summary>
public class WorkOrder : TenantEntity
{
    public string Number { get; set; } = default!;
    public Guid BranchId { get; set; }
    public Guid? CustomerOrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? DentistId { get; set; }
    public Guid ServiceItemId { get; set; }
    public WorkOrderKind Kind { get; set; } = WorkOrderKind.Original;
    public Guid? OriginalWorkOrderId { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Incomplete;
    public PriorityLevel Priority { get; set; }
    public string? PriorityReason { get; set; }

    /// <summary>Minimal patient reference (initials or file number); a full name only when the lab needs it.</summary>
    [SensitiveData]
    public string? PatientReference { get; set; }

    public string? MaterialName { get; set; }
    public string? Shade { get; set; }
    public string? Instructions { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime ReceivedAtUtc { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool DataComplete { get; set; }

    /// <summary>Comma-separated <see cref="IntakeField"/> names that are still missing.</summary>
    public string? MissingDataNotes { get; set; }

    /// <summary>Copied at intake from the price lists and never follows later list changes.</summary>
    public decimal UnitPrice { get; set; }
    public decimal QuotedPrice { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public int WorkflowVersion { get; set; }
    public Guid? CurrentStageId { get; set; }
    public Guid? CurrentAssigneeUserId { get; set; }
    public Guid? InvoiceLineId { get; set; }
    public string? CancelReason { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public Guid ClientOperationId { get; set; }
    public ICollection<WorkOrderTooth> Teeth { get; set; } = new List<WorkOrderTooth>();
    public ICollection<WorkOrderStage> Stages { get; set; } = new List<WorkOrderStage>();
    public ICollection<WorkOrderAttachment> Attachments { get; set; } = new List<WorkOrderAttachment>();
    public ICollection<WorkOrderNote> Notes { get; set; } = new List<WorkOrderNote>();
    public ICollection<WorkOrderRevision> Revisions { get; set; } = new List<WorkOrderRevision>();

    public bool IsOpen => Status is not (WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled);

    public bool IsOverdue(DateOnly today) =>
        DueDate < today && Status is not (WorkOrderStatus.ReadyForDelivery or WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled);

    /// <summary>Production has begun once any stage left the pending state.</summary>
    public bool ProductionStarted => Stages.Any(s => s.Status != StageStatus.Pending);
}

/// <summary>One tooth (FDI 11–48, or a whole arch: UPPER / LOWER) on a work order.</summary>
public class WorkOrderTooth : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public string ToothCode { get; set; } = default!;
    public string? Treatment { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A snapshot of the prescription before a change; the original is never silently replaced.</summary>
public class WorkOrderRevision : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public int RevisionNumber { get; set; }
    public string PrescriptionJson { get; set; } = default!;
    public string ChangeSummary { get; set; } = default!;
    public Guid ChangedByUserId { get; set; }
    public Guid? RequestedByDentistId { get; set; }
}

public class WorkOrderAttachment : TenantEntity, ISoftDeletable
{
    public Guid WorkOrderId { get; set; }
    public Guid FileId { get; set; }
    public FileMetadata File { get; set; } = default!;

    /// <summary>photo | stl | pdf | scan</summary>
    public string Kind { get; set; } = default!;
    public bool VisibleToDentist { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

public class WorkOrderNote : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public string Text { get; set; } = default!;
    public bool IsInternal { get; set; } = true;
}

/// <summary>A stored file. The bytes live in private storage under <see cref="StorageKey"/>, never under wwwroot.</summary>
public class FileMetadata : TenantEntity
{
    public string OriginalName { get; set; } = default!;
    public string StorageKey { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;

    /// <summary>Pending | Clean | Rejected</summary>
    public string ScanStatus { get; set; } = "Pending";
}

/// <summary>
/// A production route template (ADM-15/16). Stages with the same <see cref="WorkflowStageDefinition.Step"/> run in parallel.
/// Work orders copy the stages when they are created, so editing a template only affects new work orders.
/// </summary>
public class WorkflowDefinition : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Used for services that don't name a workflow. One per lab.</summary>
    public bool IsDefault { get; set; }

    // Data a work order needs before production may start (plan 5.4: "عدم بدء الإنتاج مع نقص بيانات إلزامية").
    public bool RequireDentist { get; set; } = true;
    public bool RequireTeeth { get; set; } = true;
    public bool RequireShade { get; set; }
    public bool RequireMaterial { get; set; }
    public bool RequirePatientReference { get; set; }
    public bool RequireAttachment { get; set; }
    public ICollection<WorkflowStageDefinition> Stages { get; set; } = new List<WorkflowStageDefinition>();
}

public class WorkflowStageDefinition : TenantEntity
{
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>1-based position. Stages sharing a step run in parallel; the next step opens when all of them finish.</summary>
    public int Step { get; set; }
    public string Name { get; set; } = default!;
    public Guid? DepartmentId { get; set; }
    public decimal TargetHours { get; set; }
    public bool RequiresQualityPass { get; set; }
}

/// <summary>
/// A stage of one work order, copied from its workflow at intake. It doubles as the production task (ORD-11/12/13):
/// one assignee, start/pause/complete, and time entries.
/// </summary>
public class WorkOrderStage : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = default!;
    public Guid? StageDefinitionId { get; set; }
    public int Step { get; set; }
    public string Name { get; set; } = default!;
    public Guid? DepartmentId { get; set; }
    public decimal TargetHours { get; set; }
    public bool RequiresQualityPass { get; set; }
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public Guid? AssignedUserId { get; set; }

    /// <summary>When the stage became workable (its step opened). Start minus this is waiting time.</summary>
    public DateTime? AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? SkipReason { get; set; }
    public ICollection<TaskTimeEntry> TimeEntries { get; set; } = new List<TaskTimeEntry>();

    public bool IsFinished => Status is StageStatus.Done or StageStatus.Skipped;
}

public enum TransitionAction { Created, DataCompleted, Started, Paused, Resumed, Completed, Skipped, MovedBack, Held, Released, Cancelled, Assigned, Revised }

/// <summary>Every workflow movement, with who, when and the before/after state. Never edited or deleted.</summary>
public class StageTransitionLog : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public TransitionAction Action { get; set; }
    public Guid? FromStageId { get; set; }
    public Guid? ToStageId { get; set; }
    public WorkOrderStatus FromStatus { get; set; }
    public WorkOrderStatus ToStatus { get; set; }
    public Guid UserId { get; set; }
    public string? Reason { get; set; }
    public DateTime AtUtc { get; set; }
}

/// <summary>Actual work on a stage. Waiting time is derived (start minus availability), not logged.</summary>
public class TaskTimeEntry : TenantEntity
{
    public Guid WorkOrderStageId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public Guid? ReasonCodeId { get; set; }

    [SensitiveData]
    public decimal LaborRateSnapshot { get; set; }

    public double Minutes(DateTime nowUtc) => ((EndedAtUtc ?? nowUtc) - StartedAtUtc).TotalMinutes;
}

public class HoldRecord : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid ReasonCodeId { get; set; }
    public string? Notes { get; set; }
    public WorkOrderStatus PreviousStatus { get; set; }
    public DateTime HeldAtUtc { get; set; }
    public Guid HeldByUserId { get; set; }
    public DateTime? ResumedAtUtc { get; set; }
    public Guid? ResumedByUserId { get; set; }
}

/// <summary>ADM-18: reasons for holds, rejections, remakes, waste and cancellations.</summary>
public class ReasonCode : TenantEntity
{
    public ReasonScope Scope { get; set; }
    public ReasonCategory Category { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

/// <summary>Prescription fields a workflow can make mandatory.</summary>
public enum IntakeField { Dentist, Teeth, Shade, Material, PatientReference, Attachment }

/// <summary>FDI tooth notation: permanent 11–48, primary 51–85, or a whole arch (UPPER / LOWER).</summary>
public static class Fdi
{
    public const string UpperArch = "UPPER";
    public const string LowerArch = "LOWER";

    public static readonly IReadOnlyList<string> Permanent =
        [.. new[] { 1, 2, 3, 4 }.SelectMany(q => Enumerable.Range(1, 8).Select(t => $"{q}{t}"))];

    public static readonly IReadOnlyList<string> Primary =
        [.. new[] { 5, 6, 7, 8 }.SelectMany(q => Enumerable.Range(1, 5).Select(t => $"{q}{t}"))];

    /// <summary>Upper-case, trimmed code if valid; otherwise null.</summary>
    public static string? Normalize(string? code)
    {
        var c = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(c))
        {
            return null;
        }

        return c is UpperArch or LowerArch || Permanent.Contains(c) || Primary.Contains(c) ? c : null;
    }

    /// <summary>Sort key: upper right → upper left → lower left → lower right, arches first.</summary>
    public static int Order(string code) => code switch
    {
        UpperArch => -2,
        LowerArch => -1,
        _ => int.TryParse(code, out var n) ? n : 999,
    };
}
