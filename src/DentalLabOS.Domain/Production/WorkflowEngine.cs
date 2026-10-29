using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Production;

/// <summary>
/// The workflow rules of plan section 4, kept free of persistence so they can be unit tested:
/// production waits for complete data, steps run in order (stages of one step in parallel), holds and cancellations
/// need a reason, and moving back keeps history (every call returns the log entry to store).
/// Callers load the work order with its stages.
/// </summary>
public static class WorkflowEngine
{
    /// <summary>Copies the template's stages onto a new work order and opens the first step.</summary>
    public static void CopyStages(WorkOrder wo, WorkflowDefinition workflow)
    {
        if (workflow.Stages.Count == 0)
        {
            throw new DomainException("Errors.Workflow.NoStages");
        }

        wo.WorkflowDefinitionId = workflow.Id;
        wo.WorkflowVersion = workflow.Version;
        foreach (var s in workflow.Stages.OrderBy(s => s.Step).ThenBy(s => s.Name, StringComparer.Ordinal))
        {
            wo.Stages.Add(new WorkOrderStage
            {
                WorkOrderId = wo.Id,
                StageDefinitionId = s.Id,
                Step = s.Step,
                Name = s.Name,
                DepartmentId = s.DepartmentId,
                TargetHours = s.TargetHours,
                RequiresQualityPass = s.RequiresQualityPass,
                QualityTemplateId = s.QualityTemplateId,
            });
        }
    }

    public static IReadOnlyList<IntakeField> MissingData(WorkflowDefinition? workflow, WorkOrder wo, int attachmentCount)
    {
        var missing = new List<IntakeField>();
        if (workflow is null)
        {
            return missing;
        }

        if (workflow.RequireDentist && wo.DentistId is null)
        {
            missing.Add(IntakeField.Dentist);
        }

        if (workflow.RequireTeeth && wo.Teeth.Count == 0)
        {
            missing.Add(IntakeField.Teeth);
        }

        if (workflow.RequireShade && string.IsNullOrWhiteSpace(wo.Shade))
        {
            missing.Add(IntakeField.Shade);
        }

        if (workflow.RequireMaterial && string.IsNullOrWhiteSpace(wo.MaterialName))
        {
            missing.Add(IntakeField.Material);
        }

        if (workflow.RequirePatientReference && string.IsNullOrWhiteSpace(wo.PatientReference))
        {
            missing.Add(IntakeField.PatientReference);
        }

        if (workflow.RequireAttachment && attachmentCount == 0)
        {
            missing.Add(IntakeField.Attachment);
        }

        return missing;
    }

    /// <summary>
    /// Records what's missing. Before production an incomplete order waits as <see cref="WorkOrderStatus.Incomplete"/>;
    /// once data is complete it becomes <see cref="WorkOrderStatus.Ready"/>.
    /// </summary>
    public static StageTransitionLog? ApplyDataCheck(WorkOrder wo, IReadOnlyList<IntakeField> missing, Guid userId, DateTime now)
    {
        wo.DataComplete = missing.Count == 0;
        wo.MissingDataNotes = missing.Count == 0 ? null : string.Join(",", missing);

        if (wo.DataComplete && wo.Status == WorkOrderStatus.Incomplete)
        {
            OpenNextStep(wo, now);
            return Log(wo, TransitionAction.DataCompleted, WorkOrderStatus.Ready, userId, now, null);
        }

        if (!wo.DataComplete && wo.Status == WorkOrderStatus.Ready && !wo.ProductionStarted)
        {
            return Log(wo, TransitionAction.Revised, WorkOrderStatus.Incomplete, userId, now, null);
        }

        return null;
    }

    public static int? CurrentStep(WorkOrder wo) =>
        wo.Stages.Where(s => !s.IsFinished).Select(s => (int?)s.Step).Min();

    /// <summary>Stages that can be worked on now: the unfinished stages of the current step.</summary>
    public static IEnumerable<WorkOrderStage> AvailableStages(WorkOrder wo)
    {
        var step = CurrentStep(wo);
        return step is null ? [] : wo.Stages.Where(s => s.Step == step && !s.IsFinished).OrderBy(s => s.Name, StringComparer.Ordinal);
    }

    public static StageTransitionLog Start(WorkOrder wo, WorkOrderStage stage, Guid userId, DateTime now)
    {
        EnsureWorkable(wo);
        if (!wo.DataComplete)
        {
            throw new DomainException("Errors.WorkOrder.DataIncomplete");
        }

        EnsureAvailable(wo, stage);
        if (stage.Status is not (StageStatus.Pending or StageStatus.Paused))
        {
            throw new DomainException("Errors.WorkOrder.StageState");
        }

        var action = stage.Status == StageStatus.Paused ? TransitionAction.Resumed : TransitionAction.Started;
        var from = wo.CurrentStageId;
        stage.Status = StageStatus.InProgress;
        stage.StartedAtUtc ??= now;
        stage.AvailableAtUtc ??= now;
        stage.AssignedUserId ??= userId;
        wo.CurrentStageId = stage.Id;
        wo.CurrentAssigneeUserId = stage.AssignedUserId;
        var log = Log(wo, action, ActiveStatus(wo), userId, now, null, stage.Id);
        log.FromStageId = from;
        return log;
    }

    public static StageTransitionLog Pause(WorkOrder wo, WorkOrderStage stage, Guid userId, DateTime now, string? reason)
    {
        EnsureWorkable(wo);
        if (stage.Status != StageStatus.InProgress)
        {
            throw new DomainException("Errors.WorkOrder.StageState");
        }

        stage.Status = StageStatus.Paused;
        return Log(wo, TransitionAction.Paused, wo.Status, userId, now, reason, stage.Id);
    }

    public static StageTransitionLog Complete(WorkOrder wo, WorkOrderStage stage, Guid userId, DateTime now)
    {
        EnsureWorkable(wo);
        EnsureAvailable(wo, stage);
        if (stage.Status is not (StageStatus.InProgress or StageStatus.Paused))
        {
            throw new DomainException("Errors.WorkOrder.StageState");
        }

        if (stage.RequiresQualityPass)
        {
            // The technician's part is done; the stage finishes only when quality accepts it (ORD-17/18).
            stage.Status = StageStatus.AwaitingQuality;
            return Log(wo, TransitionAction.SentToQuality, WorkOrderStatus.InQuality, userId, now, null, stage.Id);
        }

        stage.Status = StageStatus.Done;
        stage.CompletedAtUtc = now;
        var log = Log(wo, TransitionAction.Completed, wo.Status, userId, now, null, stage.Id);
        Advance(wo, now);
        log.ToStatus = wo.Status;
        log.ToStageId = wo.CurrentStageId;
        return log;
    }

    public static StageTransitionLog Skip(WorkOrder wo, WorkOrderStage stage, Guid userId, DateTime now, string? reason)
    {
        EnsureWorkable(wo);
        RequireReason(reason);
        EnsureAvailable(wo, stage);
        if (stage.Status != StageStatus.Pending)
        {
            throw new DomainException("Errors.WorkOrder.StageState");
        }

        stage.Status = StageStatus.Skipped;
        stage.SkipReason = reason!.Trim();
        stage.CompletedAtUtc = now;
        var log = Log(wo, TransitionAction.Skipped, wo.Status, userId, now, reason, stage.Id);
        Advance(wo, now);
        log.ToStatus = wo.Status;
        log.ToStageId = wo.CurrentStageId;
        return log;
    }

    /// <summary>Returns the order to an earlier step. Later stages restart; history stays in the log and time entries.</summary>
    public static StageTransitionLog MoveBack(WorkOrder wo, WorkOrderStage target, Guid userId, DateTime now, string? reason)
    {
        if (wo.Status is not (WorkOrderStatus.InProduction or WorkOrderStatus.InQuality or WorkOrderStatus.ReadyForDelivery))
        {
            throw new DomainException("Errors.WorkOrder.NotActive");
        }

        if (wo.DeliveryNoteId is not null)
        {
            throw new DomainException("Errors.WorkOrder.InDelivery");
        }

        RequireReason(reason);
        var current = CurrentStep(wo) ?? int.MaxValue;
        if (target.Step >= current || !wo.Stages.Contains(target))
        {
            throw new DomainException("Errors.WorkOrder.MoveBackTarget");
        }

        return Reopen(wo, target, TransitionAction.MovedBack, userId, now, reason);
    }

    /// <summary>
    /// ORD-18: the inspector's decision on a stage awaiting quality. Acceptance finishes the stage and moves on;
    /// a rejection or adjustment request sends the work back to <paramref name="returnTo"/> (the inspected stage or an
    /// earlier one) with a reason. A failed critical item blocks acceptance.
    /// </summary>
    public static StageTransitionLog Inspect(
        WorkOrder wo, WorkOrderStage stage, QualityResult result, bool criticalFailed, WorkOrderStage? returnTo, Guid userId, DateTime now, string? reason)
    {
        if (stage.Status != StageStatus.AwaitingQuality || !wo.Stages.Contains(stage))
        {
            throw new DomainException("Errors.Quality.NotAwaiting");
        }

        if (wo.Status is not (WorkOrderStatus.InQuality or WorkOrderStatus.InProduction))
        {
            throw new DomainException("Errors.WorkOrder.NotActive");
        }

        switch (result)
        {
            case QualityResult.Accepted:
                if (criticalFailed)
                {
                    throw new DomainException("Errors.Quality.CriticalFailed");
                }

                stage.Status = StageStatus.Done;
                stage.CompletedAtUtc = now;
                var log = Log(wo, TransitionAction.QualityAccepted, wo.Status, userId, now, reason, stage.Id);
                Advance(wo, now);
                if (wo.Status != WorkOrderStatus.ReadyForDelivery)
                {
                    wo.Status = ActiveStatus(wo);
                }

                log.ToStatus = wo.Status;
                log.ToStageId = wo.CurrentStageId;
                return log;

            case QualityResult.Rejected or QualityResult.AdjustmentRequested:
                RequireReason(reason);
                if (returnTo is null || !wo.Stages.Contains(returnTo) || returnTo.Step > stage.Step)
                {
                    throw new DomainException("Errors.WorkOrder.MoveBackTarget");
                }

                return Reopen(wo, returnTo, result == QualityResult.Rejected ? TransitionAction.QualityRejected : TransitionAction.AdjustmentRequested,
                    userId, now, reason);

            default:
                throw new DomainException("Errors.Quality.Result");
        }
    }

    /// <summary>
    /// ORD-20: the original of a remake. Work still in the lab is scrapped (final <see cref="WorkOrderStatus.Rejected"/>);
    /// a delivered order keeps its status. Either way the link is logged on the original.
    /// </summary>
    public static StageTransitionLog ReplaceByRemake(WorkOrder wo, Guid userId, DateTime now, string reason)
    {
        if (wo.Status is WorkOrderStatus.Cancelled or WorkOrderStatus.Rejected or WorkOrderStatus.Draft)
        {
            throw new DomainException("Errors.WorkOrder.NotActive");
        }

        if (wo.DeliveryNoteId is not null)
        {
            throw new DomainException("Errors.WorkOrder.InDelivery");
        }

        if (wo.Status == WorkOrderStatus.Delivered)
        {
            return Log(wo, TransitionAction.ReplacedByRemake, wo.Status, userId, now, reason);
        }

        foreach (var s in wo.Stages.Where(s => s.Status == StageStatus.InProgress))
        {
            s.Status = StageStatus.Paused;
        }

        wo.CurrentAssigneeUserId = null;
        return Log(wo, TransitionAction.ReplacedByRemake, WorkOrderStatus.Rejected, userId, now, reason);
    }

    /// <summary>ORD-24: the clinic received the order.</summary>
    public static StageTransitionLog MarkDelivered(WorkOrder wo, Guid userId, DateTime now, string? receivedBy)
    {
        if (wo.Status != WorkOrderStatus.ReadyForDelivery)
        {
            throw new DomainException("Errors.WorkOrder.NotReady");
        }

        wo.DeliveredAtUtc = now;
        wo.DeliveryNoteId = null;
        return Log(wo, TransitionAction.Delivered, WorkOrderStatus.Delivered, userId, now, receivedBy);
    }

    /// <summary>In production while nothing waits for inspection; in quality otherwise.</summary>
    public static WorkOrderStatus ActiveStatus(WorkOrder wo) =>
        wo.Stages.Any(s => s.Status == StageStatus.AwaitingQuality) ? WorkOrderStatus.InQuality : WorkOrderStatus.InProduction;

    /// <summary>Restarts the work from <paramref name="target"/>'s step. History stays in the log and time entries.</summary>
    private static StageTransitionLog Reopen(WorkOrder wo, WorkOrderStage target, TransitionAction action, Guid userId, DateTime now, string? reason)
    {
        var from = wo.CurrentStageId;
        foreach (var s in wo.Stages.Where(s => s.Step >= target.Step))
        {
            s.Status = StageStatus.Pending;
            s.StartedAtUtc = null;
            s.CompletedAtUtc = null;
            s.SkipReason = null;
            s.AvailableAtUtc = s.Step == target.Step ? now : null;
        }

        wo.CompletedAtUtc = null;
        wo.CurrentStageId = target.Id;
        wo.CurrentAssigneeUserId = target.AssignedUserId;
        var log = Log(wo, action, WorkOrderStatus.InProduction, userId, now, reason, target.Id);
        log.FromStageId = from;
        return log;
    }

    public static (HoldRecord Hold, StageTransitionLog Log) Hold(WorkOrder wo, Guid reasonCodeId, string? notes, Guid userId, DateTime now)
    {
        if (!wo.IsOpen || wo.Status == WorkOrderStatus.OnHold)
        {
            throw new DomainException("Errors.WorkOrder.NotActive");
        }

        if (wo.DeliveryNoteId is not null)
        {
            throw new DomainException("Errors.WorkOrder.InDelivery");
        }

        var hold = new HoldRecord
        {
            WorkOrderId = wo.Id, ReasonCodeId = reasonCodeId, Notes = notes?.Trim(), PreviousStatus = wo.Status, HeldAtUtc = now, HeldByUserId = userId,
        };
        foreach (var s in wo.Stages.Where(s => s.Status == StageStatus.InProgress))
        {
            s.Status = StageStatus.Paused;
        }

        return (hold, Log(wo, TransitionAction.Held, WorkOrderStatus.OnHold, userId, now, notes));
    }

    public static StageTransitionLog Release(WorkOrder wo, HoldRecord hold, Guid userId, DateTime now)
    {
        if (wo.Status != WorkOrderStatus.OnHold || hold.ResumedAtUtc is not null)
        {
            throw new DomainException("Errors.WorkOrder.NotOnHold");
        }

        hold.ResumedAtUtc = now;
        hold.ResumedByUserId = userId;
        var back = hold.PreviousStatus;
        if (back == WorkOrderStatus.Incomplete && wo.DataComplete)
        {
            back = WorkOrderStatus.Ready;
        }

        return Log(wo, TransitionAction.Released, back, userId, now, null);
    }

    public static StageTransitionLog Cancel(WorkOrder wo, Guid userId, DateTime now, string? reason)
    {
        if (!wo.IsOpen)
        {
            throw new DomainException("Errors.WorkOrder.NotActive");
        }

        RequireReason(reason);
        if (wo.DeliveryNoteId is not null)
        {
            throw new DomainException("Errors.WorkOrder.InDelivery");
        }

        wo.CancelReason = reason!.Trim();
        wo.CancelledByUserId = userId;
        wo.CancelledAtUtc = now;
        foreach (var s in wo.Stages.Where(s => s.Status is StageStatus.InProgress))
        {
            s.Status = StageStatus.Paused;
        }

        return Log(wo, TransitionAction.Cancelled, WorkOrderStatus.Cancelled, userId, now, reason);
    }

    /// <summary>When the current step is finished, opens the next one; with no stages left the order is ready for delivery.</summary>
    private static void Advance(WorkOrder wo, DateTime now)
    {
        if (CurrentStep(wo) is null)
        {
            wo.Status = WorkOrderStatus.ReadyForDelivery;
            wo.CompletedAtUtc = now;
            wo.CurrentStageId = null;
            wo.CurrentAssigneeUserId = null;
            return;
        }

        OpenNextStep(wo, now);
    }

    private static void OpenNextStep(WorkOrder wo, DateTime now)
    {
        var available = AvailableStages(wo).ToList();
        foreach (var s in available)
        {
            s.AvailableAtUtc ??= now;
        }

        var current = available.FirstOrDefault(s => s.Status == StageStatus.InProgress) ?? available.FirstOrDefault();
        wo.CurrentStageId = current?.Id;
        wo.CurrentAssigneeUserId = current?.AssignedUserId;
    }

    private static void EnsureWorkable(WorkOrder wo)
    {
        if (wo.Status is not (WorkOrderStatus.Ready or WorkOrderStatus.InProduction or WorkOrderStatus.InQuality))
        {
            throw new DomainException(wo.Status == WorkOrderStatus.Incomplete ? "Errors.WorkOrder.DataIncomplete" : "Errors.WorkOrder.NotActive");
        }
    }

    private static void EnsureAvailable(WorkOrder wo, WorkOrderStage stage)
    {
        if (!AvailableStages(wo).Contains(stage))
        {
            throw new DomainException("Errors.WorkOrder.StageNotAvailable");
        }
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Errors.ReasonRequired");
        }
    }

    private static StageTransitionLog Log(
        WorkOrder wo, TransitionAction action, WorkOrderStatus to, Guid userId, DateTime now, string? reason, Guid? stageId = null)
    {
        var log = new StageTransitionLog
        {
            WorkOrderId = wo.Id, Action = action, FromStageId = wo.CurrentStageId, ToStageId = stageId ?? wo.CurrentStageId, FromStatus = wo.Status,
            ToStatus = to, UserId = userId, Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), AtUtc = now,
        };
        wo.Status = to;
        return log;
    }
}
