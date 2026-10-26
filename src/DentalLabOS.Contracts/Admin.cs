namespace DentalLabOS.Contracts.Admin;

public sealed record UserSummaryDto(
    Guid ProfileId,
    Guid UserId,
    string FullName,
    string Email,
    string? JobTitle,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime? LastLoginAtUtc);

public sealed record InvitationDto(Guid Id, string Email, string FullName, string RoleName, DateTime ExpiresAtUtc, bool IsAccepted, bool IsRevoked);

public sealed record InviteUserRequest(string Email, string FullName, Guid RoleId);

/// <summary>Returned once when an invitation is created; the raw token is never stored.</summary>
public sealed record InviteUserResult(Guid InvitationId, string AcceptUrlToken, DateTime ExpiresAtUtc);

public sealed record RoleDto(Guid Id, string Name, string? SystemKey, string? Description, bool IsSystemRole, int PermissionCount, int UserCount);

public sealed record PermissionItemDto(string Key, string Module, bool Granted);

public sealed record RolePermissionsDto(Guid RoleId, string RoleName, bool IsLocked, IReadOnlyList<PermissionItemDto> Permissions, string RowVersion);

public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> PermissionKeys, string RowVersion);

public sealed record CreateRoleRequest(string Name, string? Description, Guid? CopyFromRoleId);

public sealed record AuditLogDto(Guid Id, DateTime CreatedAtUtc, string? UserName, string Action, string EntityType, Guid? EntityId, string? ChangesJson, string? Reason, string? IpAddress);

public sealed record AuditQuery(int Page = 1, int PageSize = 25, string? Action = null, string? EntityType = null, DateOnly? From = null, DateOnly? To = null);

public sealed record DocumentSequenceDto(Guid Id, string DocumentType, string? BranchName, int Year, string Prefix, int NextNumber, int Padding, bool ResetYearly, string Preview, string RowVersion);

public sealed record UpdateSequenceRequest(string Prefix, int Padding, bool ResetYearly, string RowVersion);

public sealed record LabDashboardDto(
    string TenantName,
    string TenantStatus,
    string PlanName,
    DateOnly? SubscriptionEndsOn,
    int ActiveUsers,
    int MaxUsers,
    int Branches,
    int MaxBranches,
    int Departments,
    int PendingInvitations,
    int Roles,
    IReadOnlyList<AuditLogDto> RecentActivity,
    IReadOnlyList<SetupStepDto> SetupSteps);

public sealed record SetupStepDto(string Key, bool Done, string Route);
