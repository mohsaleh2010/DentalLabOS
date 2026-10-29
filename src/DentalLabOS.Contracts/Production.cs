namespace DentalLabOS.Contracts.Production;

// Enum values travel as their names. WorkOrder statuses: Incomplete, Ready, InProduction, OnHold, InQuality, Rejected,
// Completed, ReadyForDelivery, Delivered, Cancelled. Stage statuses: Pending, InProgress, Paused, AwaitingQuality, Done, Skipped.
// Priorities: Normal, High, Urgent.

// ---- Workflows (ADM-15/16) ----

public sealed record WorkflowListItemDto(
    Guid Id, string Name, string? Description, int Version, bool IsActive, bool IsDefault, int StageCount, int ServiceCount, int OpenWorkOrders);

public sealed record WorkflowStageDto(
    Guid Id, int Step, string Name, Guid? DepartmentId, string? DepartmentName, decimal TargetHours, bool RequiresQualityPass, Guid? QualityTemplateId = null,
    string? QualityTemplateName = null);

public sealed record WorkflowDto(
    Guid Id, string Name, string? Description, int Version, bool IsActive, bool IsDefault, bool RequireDentist, bool RequireTeeth,
    bool RequireShade, bool RequireMaterial, bool RequirePatientReference, bool RequireAttachment, IReadOnlyList<WorkflowStageDto> Stages,
    string RowVersion);

/// <summary>Stages are replaced as a whole. Equal <see cref="Step"/> values run in parallel; steps are renumbered 1..n.</summary>
/// <summary>A null <see cref="QualityTemplateId"/> on a quality stage uses the lab's default checklist.</summary>
public sealed record WorkflowStageRequest(
    int Step, string Name, Guid? DepartmentId = null, decimal TargetHours = 0, bool RequiresQualityPass = false, Guid? Id = null, Guid? QualityTemplateId = null);

public sealed record WorkflowUpsertRequest(
    string Name, IReadOnlyList<WorkflowStageRequest> Stages, string? Description = null, bool IsActive = true, bool IsDefault = false,
    bool RequireDentist = true, bool RequireTeeth = true, bool RequireShade = false, bool RequireMaterial = false,
    bool RequirePatientReference = false, bool RequireAttachment = false, string? RowVersion = null);

// ---- Reason codes (ADM-18) ----

/// <summary>Scopes: Rejection, Adjustment, Remake, Hold, Waste, Cancellation, StockAdjustment, DeliveryFailure. Categories: DentistData, Design, Manufacturing, Material, Transport, Unspecified.</summary>
public sealed record ReasonCodeDto(Guid Id, string Scope, string Category, string Name, bool IsActive, int UseCount, string RowVersion);

public sealed record ReasonCodeUpsertRequest(string Scope, string Category, string Name, bool IsActive = true, string? RowVersion = null);

// ---- Technicians (ADM-07) ----

public sealed record TechnicianProfileDto(
    Guid UserProfileId, string FullName, bool HasProfile, Guid? DepartmentId, string? DepartmentName, decimal? LaborRatePerHour, string? Skills,
    decimal DifficultyCapacityPerDay, int OpenTasks, string? RowVersion);

/// <summary>A null <see cref="LaborRatePerHour"/> keeps the current rate (it is hidden without the costing permission).</summary>
public sealed record TechnicianProfileRequest(Guid DepartmentId, decimal? LaborRatePerHour, string? Skills = null, decimal DifficultyCapacityPerDay = 0, string? RowVersion = null);

public sealed record TechnicianOptionDto(Guid UserId, string FullName, Guid? DepartmentId, string? DepartmentName, int OpenTasks);

// ---- Branch context (AUTH-08) ----

public sealed record BranchOptionDto(Guid Id, string Code, string Name, bool IsMain);

// ---- Customer orders (ORD-01..03) ----

public sealed record CustomerOrderListItemDto(
    Guid Id, string Number, DateOnly DocumentDate, string CustomerName, string? DentistName, string Priority, DateOnly? RequestedDeliveryDate,
    int WorkOrderCount, int OpenWorkOrders, string Status);

public sealed record CustomerOrderDto(
    Guid Id, string Number, DateOnly DocumentDate, Guid BranchId, string BranchName, Guid CustomerId, string CustomerName, Guid? DentistId, string? DentistName,
    DateTime ReceivedAtUtc, DateOnly? RequestedDeliveryDate, string Priority, string? DeliveryInstructions, string? Notes, string Status,
    IReadOnlyList<WorkOrderListItemDto> WorkOrders, decimal? Total);

public sealed record CustomerOrderCreateRequest(
    Guid CustomerId, Guid ClientOperationId, Guid? DentistId = null, DateOnly? RequestedDeliveryDate = null, string Priority = "Normal",
    string? DeliveryInstructions = null, string? Notes = null, Guid? BranchId = null);

// ---- Work orders (ORD-04..09, 14, 15, 26..28) ----

public sealed record WorkOrderListItemDto(
    Guid Id, string Number, string CustomerName, string? DentistName, string ServiceName, string Status, string Priority, DateOnly DueDate,
    bool IsOverdue, string? CurrentStageName, string? AssigneeName, bool DataComplete, string Kind, int Quantity, DateTime ReceivedAtUtc,
    string? PatientReference);

public sealed record ToothDto(string Code, string? Treatment);

public sealed record ToothRequest(string Code, string? Treatment = null);

/// <summary>
/// ORD-05 quick intake. Missing mandatory data (defined by the service's workflow) saves the order as Incomplete;
/// <see cref="ClientOperationId"/> makes a retried submit return the first result instead of a duplicate.
/// </summary>
public sealed record WorkOrderIntakeRequest(
    Guid CustomerId, Guid ServiceItemId, Guid ClientOperationId, Guid? DentistId = null, Guid? CustomerOrderId = null, string? PatientReference = null,
    string? MaterialName = null, string? Shade = null, string? Instructions = null, IReadOnlyList<ToothRequest>? Teeth = null, int? Quantity = null,
    DateOnly? DueDate = null, string Priority = "Normal", string? PriorityReason = null, Guid? BranchId = null);

/// <summary>An intake picker entry: the service with its standard lead time and the fields its workflow requires.</summary>
public sealed record IntakeServiceDto(Guid Id, string Sku, string Name, decimal LeadHours, string? WorkflowName, IReadOnlyList<string> Required);

public sealed record WorkOrderIntakeResult(
    Guid Id, string Number, string Status, IReadOnlyList<string> MissingData, IReadOnlyList<string> Warnings, Guid? CustomerOrderId);

/// <summary>After production starts, <see cref="ChangeSummary"/> is required and the previous prescription is kept as a revision.</summary>
public sealed record WorkOrderUpdateRequest(
    Guid? DentistId, string? PatientReference, string? MaterialName, string? Shade, string? Instructions, IReadOnlyList<ToothRequest> Teeth,
    int Quantity, DateOnly DueDate, string Priority = "Normal", string? PriorityReason = null, string? ChangeSummary = null, string? RowVersion = null);

public sealed record WorkOrderStageDto(
    Guid Id, int Step, string Name, string? DepartmentName, Guid? DepartmentId, string Status, bool IsAvailable, Guid? AssignedUserId, string? AssigneeName,
    decimal TargetHours, DateTime? AvailableAtUtc, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, double WorkedMinutes, double? WaitingMinutes,
    bool IsOverTarget, string? SkipReason, bool RequiresQualityPass);

public sealed record AttachmentDto(Guid Id, string FileName, string Kind, string ContentType, long SizeBytes, bool VisibleToDentist, DateTime UploadedAtUtc, string? UploadedBy);

public sealed record NoteDto(Guid Id, string Text, bool IsInternal, DateTime CreatedAtUtc, string? Author);

public sealed record HoldDto(Guid Id, string Reason, string? Notes, DateTime HeldAtUtc, string? HeldBy);

/// <summary>Prices are null when the caller may not read price lists.</summary>
public sealed record WorkOrderDto(
    Guid Id, string Number, Guid BranchId, string BranchName, Guid? CustomerOrderId, string? CustomerOrderNumber, Guid CustomerId, string CustomerName,
    Guid? DentistId, string? DentistName, Guid ServiceItemId, string ServiceSku, string ServiceName, string Kind, string Status, string Priority,
    string? PriorityReason, string? PatientReference, string? MaterialName, string? Shade, string? Instructions, int Quantity, DateTime ReceivedAtUtc,
    DateOnly DueDate, bool IsOverdue, int DaysLate, bool DataComplete, IReadOnlyList<string> MissingData, decimal? UnitPrice, decimal? QuotedPrice,
    string? WorkflowName, int WorkflowVersion, Guid? CurrentStageId, string? CurrentStageName, string? CurrentAssigneeName, int RevisionNumber,
    string? CancelReason, DateTime? CompletedAtUtc, IReadOnlyList<ToothDto> Teeth, IReadOnlyList<WorkOrderStageDto> Stages,
    IReadOnlyList<AttachmentDto> Attachments, IReadOnlyList<NoteDto> Notes, HoldDto? ActiveHold, string RowVersion,
    Guid? OriginalWorkOrderId = null, string? OriginalNumber = null, Guid? DeliveryNoteId = null, string? DeliveryNoteNumber = null,
    DateTime? DeliveredAtUtc = null, int QualityChecks = 0, int OpenAdjustments = 0, int Remakes = 0);

public sealed record TimelineEntryDto(DateTime AtUtc, string Action, string FromStatus, string ToStatus, string? StageName, string? UserName, string? Reason);

public sealed record RevisionDto(Guid Id, int RevisionNumber, string ChangeSummary, DateTime CreatedAtUtc, string? ChangedBy, string PrescriptionJson);

public sealed record NoteRequest(string Text, bool IsInternal = true);

public sealed record StageActionRequest(string? Reason = null);

public sealed record HoldRequest(Guid ReasonCodeId, string? Notes = null);

public sealed record CancelRequest(Guid ReasonCodeId, string? Notes = null);

public sealed record AssignRequest(IReadOnlyList<Guid> StageIds, Guid? UserId);

public sealed record AssignResultDto(int Assigned, int Skipped);

public sealed record ScanResultDto(string Kind, Guid Id, string Number);

// ---- Production (ORD-10..13, 16, 29) ----

public sealed record TaskDto(
    Guid StageId, Guid WorkOrderId, string Number, string CustomerName, string ServiceName, string StageName, string StageStatus, Guid? DepartmentId,
    string? DepartmentName, Guid? AssignedUserId, string? AssigneeName, string Priority, DateOnly DueDate, bool IsOverdue, DateTime? AvailableAtUtc,
    DateTime? StartedAtUtc, decimal TargetHours, double ElapsedHours, int TeethCount, bool IsAvailable);

public sealed record MyTasksDto(IReadOnlyList<TaskDto> Mine, IReadOnlyList<TaskDto> Available, Guid? DepartmentId, string? DepartmentName);

public sealed record BoardCardDto(
    Guid Id, string Number, string CustomerName, string ServiceName, string Priority, DateOnly DueDate, bool IsOverdue, string? StageName,
    string? StageStatus, string? AssigneeName, int MissingCount, int TeethCount);

/// <summary>Kinds: incomplete, ready, stage, hold, done. Stage columns are named after the current stage.</summary>
public sealed record BoardColumnDto(string Key, string Kind, string Title, IReadOnlyList<BoardCardDto> Cards);

public sealed record BoardDto(IReadOnlyList<BoardColumnDto> Columns, int Total, int Overdue);

public sealed record OverdueDto(IReadOnlyList<WorkOrderListItemDto> PastDue, IReadOnlyList<TaskDto> OverTarget);

public sealed record CalendarDayDto(DateOnly Date, int Due, int Done, int Overdue, int Urgent);

public sealed record CalendarDto(int Year, int Month, IReadOnlyList<CalendarDayDto> Days, int Capacity);
