using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Production;

/// <summary>
/// ORD-22: a delivery note for one clinic. Lines are work orders that passed quality; each line's packaging and label
/// are checked before it leaves (plan 5.9). Draft → out for delivery → delivered, or failed (orders return to ready).
/// </summary>
public class DeliveryNote : TenantEntity
{
    public string Number { get; set; } = default!;
    public Guid BranchId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public Guid ClientOperationId { get; set; }
    public Guid CustomerId { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Draft;

    /// <summary>Copied when the note is made, so a later customer edit doesn't change a printed note.</summary>
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }

    public Guid? RunId { get; set; }
    public int RunSequence { get; set; }

    public DateTime? DispatchedAtUtc { get; set; }
    public Guid? DeliveredByUserId { get; set; }
    public string? ReceivedByName { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public Guid? FailureReasonCodeId { get; set; }
    public string? FailureNotes { get; set; }
    public Guid? ProofFileId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();

    public bool IsActive => Status is DeliveryStatus.Draft or DeliveryStatus.OutForDelivery;
}

public class DeliveryNoteLine : TenantEntity
{
    public Guid DeliveryNoteId { get; set; }
    public Guid WorkOrderId { get; set; }

    /// <summary>Packaging and label checked (plan 5.9).</summary>
    public bool PackagingChecked { get; set; }
    public Guid? CheckedByUserId { get; set; }
}

/// <summary>ORD-23: a courier's round for one day, with notes in visiting order.</summary>
public class DeliveryRun : TenantEntity
{
    public DateOnly RunDate { get; set; }
    public Guid? CourierUserId { get; set; }
    public string? CourierName { get; set; }
    public DeliveryRunStatus Status { get; set; } = DeliveryRunStatus.Planned;
    public string? Notes { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
