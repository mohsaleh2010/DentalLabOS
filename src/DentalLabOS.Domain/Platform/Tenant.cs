using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Platform;

/// <summary>A laboratory (customer of the SaaS). Owns every tenant-scoped row.</summary>
public class Tenant : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public TenantStatus Status { get; set; } = TenantStatus.Trial;
    public string DefaultCulture { get; set; } = "ar-EG";
    public string BaseCurrencyCode { get; set; } = "EGP";
    public string TimeZoneId { get; set; } = "Africa/Cairo";
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.MovingAverage;
    public bool AllowNegativeStock { get; set; }
    public bool AllowSelfApproval { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public string? SuspendReason { get; set; }
    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();

    public bool CanSignIn => Status is TenantStatus.Trial or TenantStatus.Active;

    public void Suspend(string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Errors.ReasonRequired");
        }

        Status = TenantStatus.Suspended;
        SuspendedAtUtc = nowUtc;
        SuspendReason = reason.Trim();
    }

    public void Activate()
    {
        if (Status == TenantStatus.Closed)
        {
            throw new DomainException("Errors.Tenant.Closed");
        }

        Status = TenantStatus.Active;
        SuspendedAtUtc = null;
        SuspendReason = null;
    }
}

public class SubscriptionPlan : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public int MaxUsers { get; set; }
    public int MaxBranches { get; set; }
    public int MaxMonthlyWorkOrders { get; set; }
    public long MaxStorageBytes { get; set; }
    public bool IncludesAccounting { get; set; }
    public bool IncludesPortal { get; set; }
    public bool IncludesAi { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class TenantSubscription : Entity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? GraceEndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class FeatureFlag : Entity
{
    public string Key { get; set; } = default!;
    public string? Description { get; set; }
    public bool EnabledGlobally { get; set; }
    public ICollection<TenantFeatureFlag> Tenants { get; set; } = new List<TenantFeatureFlag>();
}

public class TenantFeatureFlag : Entity
{
    public Guid TenantId { get; set; }
    public Guid FeatureFlagId { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>Temporary, audited support access to a tenant (metadata only by default).</summary>
public class SupportAccessGrant : Entity
{
    public Guid TenantId { get; set; }
    public Guid SupportUserId { get; set; }
    public string Reason { get; set; } = default!;
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Scope { get; set; } = "metadata-only";
    public Guid ApprovedByTenantUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
