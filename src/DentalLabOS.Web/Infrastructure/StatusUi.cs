using DentalLabOS.SharedUI.Components;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Maps statuses to label key + tone + icon so every status reads the same everywhere.</summary>
public static class StatusUi
{
    public sealed record Look(string LabelKey, BadgeTone Tone, string Icon);

    public static Look Tenant(string status) => status switch
    {
        "Active" => new("TenantStatus.Active", BadgeTone.Success, "circle-check"),
        "Trial" => new("TenantStatus.Trial", BadgeTone.Info, "clock"),
        "Suspended" => new("TenantStatus.Suspended", BadgeTone.Warning, "ban"),
        _ => new("TenantStatus.Closed", BadgeTone.Neutral, "lock"),
    };

    public static Look Active(bool active) => active
        ? new("Common.Active", BadgeTone.Success, "circle-check")
        : new("Common.Inactive", BadgeTone.Neutral, "ban");

    public static string AuditIcon(string action) => action switch
    {
        "Create" => "plus",
        "Update" => "pencil",
        "Delete" => "x",
        "Login" => "log-out",
        "LoginFailed" => "triangle-alert",
        "PermissionChange" or "RoleAssignment" => "shield-check",
        "Invite" or "InviteAccepted" => "mail",
        "StatusChange" => "refresh-cw",
        _ => "activity",
    };
}
