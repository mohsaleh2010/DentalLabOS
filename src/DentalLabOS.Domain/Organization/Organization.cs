using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Organization;

public class Branch : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Department : TenantEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    [SensitiveData]
    public decimal? LaborRatePerHour { get; set; }

    public bool IsProduction { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Lab-specific profile of a tenant user (the login itself lives in Identity).</summary>
public class UserProfile : TenantEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? JobTitle { get; set; }
    public string? DefaultPage { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresMfa { get; set; }
    public ICollection<UserBranchScope> BranchScopes { get; set; } = new List<UserBranchScope>();
    public ICollection<UserDepartmentScope> DepartmentScopes { get; set; } = new List<UserDepartmentScope>();
    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}

public class UserBranchScope : TenantEntity
{
    public Guid UserProfileId { get; set; }
    public Guid BranchId { get; set; }
}

public class UserDepartmentScope : TenantEntity
{
    public Guid UserProfileId { get; set; }
    public Guid DepartmentId { get; set; }
}

public class TechnicianProfile : TenantEntity
{
    public Guid UserProfileId { get; set; }
    public Guid DepartmentId { get; set; }

    [SensitiveData]
    public decimal LaborRatePerHour { get; set; }

    public string? Skills { get; set; }
    public decimal DifficultyCapacityPerDay { get; set; }
}

public class AppRole : TenantEntity
{
    public string Name { get; set; } = default!;

    /// <summary>Stable key for built-in roles (e.g. "owner"); null for custom roles.</summary>
    public string? SystemKey { get; set; }

    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

/// <summary>Global catalog of permission keys (not tenant-owned).</summary>
public class Permission : Entity
{
    public string Key { get; set; } = default!;
    public string Module { get; set; } = default!;
    public string Description { get; set; } = default!;
}

public class RolePermission : TenantEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

/// <summary>Links a tenant user profile to a role.</summary>
public class UserRole : TenantEntity
{
    public Guid UserProfileId { get; set; }
    public Guid RoleId { get; set; }
    public AppRole Role { get; set; } = default!;
}

public class UserInvitation : TenantEntity
{
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public Guid RoleId { get; set; }

    [SensitiveData]
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsUsable(DateTime nowUtc) => AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}

/// <summary>Gap-free numbering per tenant, document type, branch and year (rule A2.1-3).</summary>
public class DocumentSequence : TenantEntity
{
    public string DocumentType { get; set; } = default!;
    public Guid? BranchId { get; set; }
    public int Year { get; set; }
    public string Prefix { get; set; } = default!;
    public int NextNumber { get; set; } = 1;
    public int Padding { get; set; } = 6;
    public bool ResetYearly { get; set; } = true;

    public string Format(int number) =>
        ResetYearly
            ? $"{Prefix}-{Year % 100:00}-{number.ToString().PadLeft(Padding, '0')}"
            : $"{Prefix}-{number.ToString().PadLeft(Padding, '0')}";
}

public class ApprovalPolicy : TenantEntity
{
    public string DocumentType { get; set; } = default!;
    public string Trigger { get; set; } = default!;
    public decimal? Threshold { get; set; }
    public Guid ApproverRoleId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ApprovalRequest : TenantEntity
{
    public string DocumentType { get; set; } = default!;
    public Guid DocumentId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Reason { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>Segregation of duties (A3.2): the requester cannot approve unless the tenant allows it.</summary>
    public void Decide(Guid deciderUserId, bool approve, string? reason, bool allowSelfApproval, DateTime nowUtc)
    {
        if (Status != ApprovalStatus.Pending)
        {
            throw new DomainException("Errors.Approval.AlreadyDecided");
        }

        if (deciderUserId == RequestedByUserId && !allowSelfApproval)
        {
            throw new DomainException("Errors.Approval.SelfApproval");
        }

        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Errors.ReasonRequired");
        }

        Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        DecidedByUserId = deciderUserId;
        DecidedAtUtc = nowUtc;
        Reason = reason;
    }
}
