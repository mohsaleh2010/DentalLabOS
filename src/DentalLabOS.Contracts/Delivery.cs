namespace DentalLabOS.Contracts.Production;

// Delivery note statuses: Draft, OutForDelivery, Delivered, Failed, Cancelled. Run statuses: Planned, OnRoute, Completed.

/// <summary>ORD-21: orders that passed quality and are not on a delivery note yet, grouped by clinic.</summary>
public sealed record ReadyGroupDto(Guid CustomerId, string CustomerName, string? Address, string? Phone, IReadOnlyList<WorkOrderListItemDto> WorkOrders);

public sealed record DeliveryNoteListItemDto(
    Guid Id, string Number, DateOnly DocumentDate, Guid CustomerId, string CustomerName, string Status, int LineCount, int CheckedCount,
    Guid? RunId, string? RunLabel, int RunSequence, DateTime? DeliveredAtUtc, string? ReceivedByName);

public sealed record DeliveryNoteLineDto(
    Guid Id, Guid WorkOrderId, string Number, string ServiceName, string? PatientReference, int Quantity, IReadOnlyList<string> Teeth,
    bool PackagingChecked, string WorkOrderStatus);

public sealed record DeliveryNoteDto(
    Guid Id, string Number, DateOnly DocumentDate, string Status, Guid BranchId, string BranchName, Guid CustomerId, string CustomerName, string? Address,
    string? ContactPhone, string? Notes, Guid? RunId, string? RunLabel, int RunSequence, DateTime? DispatchedAtUtc, DateTime? DeliveredAtUtc,
    string? ReceivedByName, string? DeliveredBy, string? FailureReason, string? FailureNotes, bool HasProof, IReadOnlyList<DeliveryNoteLineDto> Lines,
    string RowVersion);

/// <summary>All orders must belong to <see cref="CustomerId"/>, be ready for delivery and not be on another active note.</summary>
public sealed record DeliveryNoteCreateRequest(Guid CustomerId, IReadOnlyList<Guid> WorkOrderIds, Guid ClientOperationId, string? Notes = null, Guid? RunId = null);

public sealed record PackagingCheckRequest(IReadOnlyList<Guid> LineIds, bool Checked = true);

/// <summary>ORD-24: either who received it, or why it could not be delivered (a DeliveryFailure reason). The orders of a failed note return to ready.</summary>
public sealed record DeliveryConfirmRequest(
    bool Delivered, string? ReceivedByName = null, DateTime? DeliveredAtUtc = null, Guid? FailureReasonCodeId = null, string? FailureNotes = null,
    string? RowVersion = null);

public sealed record DeliveryRunListItemDto(
    Guid Id, DateOnly RunDate, string Courier, string Status, int StopCount, int Delivered, int Failed, DateTime? StartedAtUtc, DateTime? CompletedAtUtc);

public sealed record DeliveryRunDto(
    Guid Id, DateOnly RunDate, Guid? CourierUserId, string Courier, string Status, string? Notes, DateTime? StartedAtUtc, DateTime? CompletedAtUtc,
    IReadOnlyList<DeliveryNoteListItemDto> Stops, string RowVersion);

/// <summary>Either a lab user or a free-text courier name (an outside driver).</summary>
public sealed record DeliveryRunRequest(DateOnly RunDate, Guid? CourierUserId = null, string? CourierName = null, string? Notes = null, string? RowVersion = null);

/// <summary>The run's stops in visiting order; notes left out are taken off the run.</summary>
public sealed record RunStopsRequest(IReadOnlyList<Guid> NoteIds);
