using DentalLabOS.Domain.Security;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>
/// Sidebar model. Items are shown only when the user holds the permission. Items of later phases are shown
/// disabled with their phase so each role sees the roadmap that concerns it (no route exists yet for them).
/// </summary>
public static class Navigation
{
    public sealed record Item(string TitleKey, string Href, string Icon, string Permission, int? Phase = null)
    {
        public bool IsSoon => Phase is not null;
    }

    public sealed record Section(string TitleKey, IReadOnlyList<Item> Items);

    public static readonly IReadOnlyList<Section> Platform =
    [
        new("Nav.Section.Platform",
        [
            new("Nav.PlatformDashboard", "/platform", "gauge", Permissions.Platform.DashboardRead),
            new("Nav.Tenants", "/platform/tenants", "building-2", Permissions.Platform.TenantsRead),
            new("Nav.Plans", "/platform/plans", "layers", Permissions.Platform.TenantsRead),
            new("Nav.PlatformAudit", "/platform/audit", "scroll-text", Permissions.Platform.AuditRead),
        ]),
        new("Nav.Section.PlatformOps",
        [
            new("Nav.Billing", "/platform/billing", "credit-card", Permissions.Platform.BillingRead, 13),
            new("Nav.SupportAccess", "/platform/support-access", "key-round", Permissions.Platform.SupportManage, 13),
            new("Nav.Health", "/platform/health", "server", Permissions.Platform.HealthRead, 13),
            new("Nav.Features", "/platform/features", "flag", Permissions.Platform.FeaturesManage, 13),
        ]),
    ];

    public static readonly IReadOnlyList<Section> Tenant =
    [
        new("Nav.Section.Home",
        [
            new("Nav.Dashboard", "/admin", "layout-dashboard", Permissions.Admin.DashboardRead),
            new("Nav.MyTasks", "/my/tasks", "list-checks", "tasks.own.read", 3),
        ]),
        new("Nav.Section.Production",
        [
            new("Nav.Intake", "/workorders/intake", "scan-line", "workorders.create", 3),
            new("Nav.WorkOrders", "/workorders", "clipboard-list", "workorders.read", 3),
            new("Nav.ProductionBoard", "/production/board", "factory", "production.read", 3),
            new("Nav.Quality", "/quality/queue", "microscope", "quality.read", 4),
            new("Nav.Delivery", "/delivery/ready", "truck", "delivery.read", 4),
        ]),
        new("Nav.Section.Parties",
        [
            new("Nav.Customers", "/customers", "stethoscope", "customers.read", 2),
            new("Nav.Suppliers", "/suppliers", "building", "suppliers.read", 2),
            new("Nav.Items", "/items", "tag", "items.read", 2),
        ]),
        new("Nav.Section.Supply",
        [
            new("Nav.Inventory", "/inventory/balances", "boxes", "inventory.read", 5),
            new("Nav.Purchasing", "/purchasing/orders", "shopping-cart", "purchasing.order.read", 6),
        ]),
        new("Nav.Section.Finance",
        [
            new("Nav.Sales", "/sales/invoices", "receipt", "sales.invoice.read", 7),
            new("Nav.Payments", "/payments/receipts", "wallet", "payments.in.read", 8),
            new("Nav.Treasury", "/treasury/accounts", "landmark", "treasury.read", 8),
            new("Nav.Expenses", "/expenses", "credit-card", "expenses.read", 9),
            new("Nav.Accounting", "/accounting/accounts", "calculator", "accounting.accounts.read", 11),
            new("Nav.Reports", "/reports", "chart-column", "reports.read", 13),
        ]),
        new("Nav.Section.Admin",
        [
            new("Nav.Branches", "/admin/branches", "building-2", Permissions.Admin.BranchesManage),
            new("Nav.Departments", "/admin/departments", "network", Permissions.Admin.DepartmentsManage),
            new("Nav.Users", "/admin/users", "users", Permissions.Admin.UsersRead),
            new("Nav.Roles", "/admin/roles", "shield-check", Permissions.Admin.RolesManage),
            new("Nav.Numbering", "/admin/numbering", "hash", Permissions.Admin.NumberingManage),
            new("Nav.Audit", "/admin/audit", "scroll-text", Permissions.Admin.AuditRead),
            new("Nav.Subscription", "/admin/subscription", "sparkles", Permissions.Admin.SubscriptionRead),
        ]),
    ];

    public static bool IsActive(string currentPath, string href) =>
        href is "/admin" or "/platform"
            ? string.Equals(currentPath.TrimEnd('/'), href, StringComparison.OrdinalIgnoreCase)
            : currentPath.StartsWith(href, StringComparison.OrdinalIgnoreCase);
}
