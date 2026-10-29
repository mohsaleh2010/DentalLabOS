namespace DentalLabOS.Contracts.Production;

// Quality results: Accepted, Rejected, AdjustmentRequested. Adjustment sources: Quality, Customer; statuses: Open, Closed.
// Remake cost bearer (a human decision): Undecided, Lab, Customer, Supplier.

// ---- Checklist templates (ADM-17) ----

public sealed record QualityTemplateListItemDto(
    Guid Id, string Name, string? Description, bool IsActive, bool IsDefault, int ItemCount, int CriticalCount, int StageCount);

public sealed record QualityTemplateItemDto(Guid Id, int Order, string Text, bool IsCritical);

public sealed record QualityTemplateDto(
    Guid Id, string Name, string? Description, bool IsActive, bool IsDefault, IReadOnlyList<QualityTemplateItemDto> Items, string RowVersion);

public sealed record QualityTemplateItemRequest(string Text, bool IsCritical = false, Guid? Id = null);

/// <summary>Items are replaced as a whole, in the order given.</summary>
public sealed record QualityTemplateUpsertRequest(
    string Name, IReadOnlyList<QualityTemplateItemRequest> Items, string? Description = null, bool IsActive = true, bool IsDefault = false,
    string? RowVersion = null);

// ---- Inspection (ORD-17/18) ----

public sealed record QualityQueueItemDto(
    Guid StageId, Guid WorkOrderId, string Number, string Kind, string CustomerName, string ServiceName, string StageName, string? DepartmentName,
    string Priority, DateOnly DueDate, bool IsOverdue, DateTime? SubmittedAtUtc, string? SubmittedBy, int TeethCount, int PreviousChecks);

public sealed record QualityCheckItemDto(string Text, bool IsCritical, bool Passed, string? Note);

public sealed record QualityCheckDto(
    Guid Id, Guid WorkOrderId, string WorkOrderNumber, string? StageName, string Result, string? ReasonName, string? Category, string? ReturnToStageName,
    string? Notes, DateTime CheckedAtUtc, string? InspectorName, IReadOnlyList<QualityCheckItemDto> Items);

/// <summary>What the inspection screen needs: the stage waiting for inspection, its checklist, where work may go back to, and earlier checks.</summary>
public sealed record QualityInspectionDto(
    WorkOrderDto WorkOrder, Guid StageId, string StageName, Guid? TemplateId, string? TemplateName, IReadOnlyList<QualityTemplateItemDto> Items,
    IReadOnlyList<WorkOrderStageDto> ReturnTargets, IReadOnlyList<QualityCheckDto> History);

public sealed record QualityCheckItemRequest(Guid TemplateItemId, bool Passed, string? Note = null);

/// <summary>
/// Accepted needs every critical item passed. Rejected and AdjustmentRequested need a reason code and the stage the work goes back to
/// (the inspected stage or an earlier one); an adjustment request is also listed under adjustments (ORD-19).
/// </summary>
public sealed record QualityCheckRequest(
    Guid StageId, string Result, IReadOnlyList<QualityCheckItemRequest> Items, Guid? ReasonCodeId = null, Guid? ReturnToStageId = null, string? Notes = null);

public sealed record QualityCheckResultDto(Guid CheckId, string WorkOrderStatus, Guid? AdjustmentId);

// ---- Adjustments (ORD-19) and remakes (ORD-20) ----

public sealed record AdjustmentDto(
    Guid Id, Guid WorkOrderId, string WorkOrderNumber, string CustomerName, string ServiceName, string Source, string Status, string ReasonName,
    string Category, string? Notes, DateTime RequestedAtUtc, string? RequestedBy, DateTime? ClosedAtUtc, Guid? AdjustmentWorkOrderId,
    string? AdjustmentWorkOrderNumber);

/// <summary>The clinic sent back a delivered case for correction: a linked adjustment work order is opened.</summary>
public sealed record CustomerAdjustmentRequest(
    Guid ReasonCodeId, Guid ClientOperationId, string? Notes = null, DateOnly? DueDate = null, string Priority = "High", string? PriorityReason = null);

public sealed record RemakeListItemDto(
    Guid Id, Guid OriginalWorkOrderId, string OriginalNumber, Guid RemakeWorkOrderId, string RemakeNumber, string RemakeStatus, string CustomerName,
    string ServiceName, string ReasonName, string Category, string ChargeTo, string? Decision, DateTime DecidedAtUtc, string? DecidedBy);

/// <summary>
/// Opens a remake: a new work order with the original prescription, linked to it. Work still in the lab is closed as rejected.
/// When the customer bears the cost the remake keeps the original price; otherwise it is priced at zero.
/// </summary>
public sealed record RemakeRequest(
    Guid OriginalWorkOrderId, Guid ReasonCodeId, string ChargeTo, Guid ClientOperationId, string? Decision = null, DateOnly? DueDate = null,
    string Priority = "High", string? PriorityReason = null);

public sealed record LinkedWorkOrderResultDto(Guid RecordId, Guid WorkOrderId, string Number);

public sealed record QualityOverviewDto(int AwaitingInspection, int CheckedToday, int RejectedToday, int OpenAdjustments, int RemakesThisMonth);
