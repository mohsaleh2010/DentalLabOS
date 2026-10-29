using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Production;

/// <summary>ADM-17: a configurable inspection checklist (plan 5.6). One template per lab can be the default.</summary>
public class QualityTemplate : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public ICollection<QualityTemplateItem> Items { get; set; } = new List<QualityTemplateItem>();
}

public class QualityTemplateItem : TenantEntity
{
    public Guid QualityTemplateId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = default!;

    /// <summary>A failed critical item blocks acceptance.</summary>
    public bool IsCritical { get; set; }
}

/// <summary>One inspection (ORD-18): who, when, the result, the reason and each checklist answer. Never edited afterwards.</summary>
public class QualityCheck : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid? StageId { get; set; }
    public string? StageName { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public Guid InspectorUserId { get; set; }
    public QualityResult Result { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public ReasonCategory? Category { get; set; }

    /// <summary>For a rejection or adjustment: the stage the work went back to.</summary>
    public Guid? ReturnToStageId { get; set; }
    public string? Notes { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public ICollection<QualityCheckItem> Items { get; set; } = new List<QualityCheckItem>();
}

/// <summary>A checklist answer; the item text is copied so later template edits don't rewrite history.</summary>
public class QualityCheckItem : TenantEntity
{
    public Guid QualityCheckId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = default!;
    public bool IsCritical { get; set; }
    public bool Passed { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// ORD-19: a correction of a case, kept apart from remakes (plan 5.6). From quality it reworks the same order;
/// from the clinic after delivery it opens a linked adjustment order.
/// </summary>
public class CaseAdjustment : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public AdjustmentSource Source { get; set; }
    public Guid? QualityCheckId { get; set; }
    public Guid? AdjustmentWorkOrderId { get; set; }
    public Guid ReasonCodeId { get; set; }
    public ReasonCategory Category { get; set; }
    public string? Notes { get; set; }
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Open;
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

/// <summary>
/// ORD-20: a remake is a new work order linked to the original. Who bears the cost is a human decision (plan A2.3),
/// never inferred from the reason.
/// </summary>
public class RemakeRecord : TenantEntity
{
    public Guid OriginalWorkOrderId { get; set; }
    public Guid RemakeWorkOrderId { get; set; }
    public Guid? QualityCheckId { get; set; }
    public Guid ReasonCodeId { get; set; }
    public ReasonCategory Category { get; set; }
    public RemakeChargeTo ChargeTo { get; set; }
    public string? Decision { get; set; }
    public Guid DecidedByUserId { get; set; }
    public DateTime DecidedAtUtc { get; set; }

    /// <summary>Filled by job costing (phase 10).</summary>
    [SensitiveData]
    public decimal CostCharged { get; set; }
}
