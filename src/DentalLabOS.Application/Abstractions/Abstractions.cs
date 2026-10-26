using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Abstractions;

/// <summary>
/// Persistence boundary used by application services. Tenant-owned sets are automatically filtered to the
/// current tenant by global query filters; bypassing them is restricted to platform services (see TENANT_ISOLATION.md).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Department> Departments { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserInvitation> UserInvitations { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the original concurrency token so the next save fails if the row changed meanwhile.</summary>
    void SetOriginalRowVersion(Entity entity, string? rowVersion);

    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    PlatformRole PlatformRole { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformStaff => PlatformRole != PlatformRole.None;
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

/// <summary>
/// The tenant every query and write is scoped to. Normally derived from the signed-in user; platform provisioning
/// may switch it explicitly with <see cref="BeginScope"/>.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool IsPlatformContext { get; }
    IDisposable BeginScope(Guid tenantId);
}

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default);
    Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> unless the current user holds the permission.</summary>
    Task DemandAsync(string permission, CancellationToken cancellationToken = default);

    void Invalidate();
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed record IdentityUserSummary(Guid UserId, string Email, string FullName, DateTime? LastLoginAtUtc, bool IsActive);

/// <summary>Boundary to ASP.NET Core Identity (which lives in Infrastructure).</summary>
public interface IIdentityService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> CreateUserAsync(string email, string fullName, string password, Guid? tenantId, PlatformRole platformRole, string culture, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IdentityUserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> AnyPlatformOwnerAsync(CancellationToken cancellationToken = default);
}

public interface IAuditWriter
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the next SaveChanges).</summary>
    void Add(string action, string entityType, Guid? entityId, string? reason = null, object? changes = null, Guid? tenantId = null);
}

public interface INumberingService
{
    /// <summary>Allocates the next document number inside the caller's transaction (gap-free on rollback).</summary>
    Task<string> NextAsync(string documentType, Guid? branchId, DateOnly date, CancellationToken cancellationToken = default);
}
