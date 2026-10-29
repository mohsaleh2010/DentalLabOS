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

    public static Look WorkOrder(string status) => status switch
    {
        "Incomplete" => new("WoStatus.Incomplete", BadgeTone.Warning, "circle-alert"),
        "Ready" => new("WoStatus.Ready", BadgeTone.Info, "inbox"),
        "InProduction" => new("WoStatus.InProduction", BadgeTone.Brand, "factory"),
        "OnHold" => new("WoStatus.OnHold", BadgeTone.Danger, "hand"),
        "InQuality" => new("WoStatus.InQuality", BadgeTone.Violet, "microscope"),
        "Rejected" => new("WoStatus.Rejected", BadgeTone.Danger, "circle-x"),
        "Completed" or "ReadyForDelivery" => new("WoStatus.ReadyForDelivery", BadgeTone.Success, "circle-check"),
        "Delivered" => new("WoStatus.Delivered", BadgeTone.Success, "truck"),
        "Cancelled" => new("WoStatus.Cancelled", BadgeTone.Neutral, "ban"),
        _ => new("WoStatus.Draft", BadgeTone.Neutral, "file-text"),
    };

    public static Look Stage(string status) => status switch
    {
        "InProgress" => new("StageStatus.InProgress", BadgeTone.Brand, "play"),
        "Paused" => new("StageStatus.Paused", BadgeTone.Warning, "pause"),
        "Done" => new("StageStatus.Done", BadgeTone.Success, "check"),
        "Skipped" => new("StageStatus.Skipped", BadgeTone.Neutral, "skip-forward"),
        "AwaitingQuality" => new("StageStatus.AwaitingQuality", BadgeTone.Violet, "microscope"),
        _ => new("StageStatus.Pending", BadgeTone.Neutral, "clock"),
    };

    public static Look Quality(string result) => result switch
    {
        "Accepted" => new("QualityResult.Accepted", BadgeTone.Success, "circle-check"),
        "Rejected" => new("QualityResult.Rejected", BadgeTone.Danger, "circle-x"),
        "AdjustmentRequested" => new("QualityResult.AdjustmentRequested", BadgeTone.Warning, "wrench"),
        _ => new("QualityResult.Pending", BadgeTone.Neutral, "clock"),
    };

    public static Look Adjustment(string status) => status == "Open"
        ? new("AdjustmentStatus.Open", BadgeTone.Warning, "wrench")
        : new("AdjustmentStatus.Closed", BadgeTone.Success, "check");

    public static Look ChargeTo(string chargeTo) => chargeTo switch
    {
        "Lab" => new("RemakeChargeTo.Lab", BadgeTone.Brand, "building-2"),
        "Customer" => new("RemakeChargeTo.Customer", BadgeTone.Info, "stethoscope"),
        "Supplier" => new("RemakeChargeTo.Supplier", BadgeTone.Violet, "package"),
        _ => new("RemakeChargeTo.Undecided", BadgeTone.Warning, "circle-help"),
    };

    public static Look Delivery(string status) => status switch
    {
        "OutForDelivery" => new("DeliveryStatus.OutForDelivery", BadgeTone.Brand, "truck"),
        "Delivered" => new("DeliveryStatus.Delivered", BadgeTone.Success, "circle-check"),
        "Failed" => new("DeliveryStatus.Failed", BadgeTone.Danger, "circle-x"),
        "Cancelled" => new("DeliveryStatus.Cancelled", BadgeTone.Neutral, "ban"),
        _ => new("DeliveryStatus.Draft", BadgeTone.Neutral, "file-text"),
    };

    public static Look Run(string status) => status switch
    {
        "OnRoute" => new("RunStatus.OnRoute", BadgeTone.Brand, "truck"),
        "Completed" => new("RunStatus.Completed", BadgeTone.Success, "circle-check"),
        _ => new("RunStatus.Planned", BadgeTone.Info, "calendar"),
    };

    public static Look Priority(string priority) => priority switch
    {
        "Urgent" => new("Priority.Urgent", BadgeTone.Danger, "alarm-clock"),
        "High" => new("Priority.High", BadgeTone.Warning, "arrow-up"),
        _ => new("Priority.Normal", BadgeTone.Neutral, "minus"),
    };

    public static string TransitionIcon(string action) => action switch
    {
        "Created" => "plus",
        "DataCompleted" => "circle-check",
        "Started" or "Resumed" => "play",
        "Paused" => "pause",
        "Completed" => "check",
        "Skipped" => "skip-forward",
        "MovedBack" => "undo-2",
        "Held" => "hand",
        "Released" => "play",
        "Cancelled" => "ban",
        "Assigned" => "user-check",
        "Revised" => "history",
        "SentToQuality" => "microscope",
        "QualityAccepted" => "circle-check",
        "QualityRejected" => "circle-x",
        "AdjustmentRequested" => "wrench",
        "ReplacedByRemake" or "RemakeOf" => "repeat",
        "AddedToDelivery" => "clipboard-list",
        "RemovedFromDelivery" => "clipboard-x",
        "OutForDelivery" => "truck",
        "Delivered" => "package-check",
        "DeliveryFailed" => "triangle-alert",
        _ => "activity",
    };

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
