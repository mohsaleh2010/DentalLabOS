using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Operations;

/// <summary>
/// Insert-only audit trail. <see cref="TenantId"/> is null for platform-level events (PLT-12).
/// </summary>
public class AuditLog : Entity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? ChangesJson { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public bool ViaSupportAccess { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string PermissionChange = "PermissionChange";
    public const string RoleAssignment = "RoleAssignment";
    public const string Invite = "Invite";
    public const string InviteAccepted = "InviteAccepted";
    public const string StatusChange = "StatusChange";
    public const string Export = "Export";
}
