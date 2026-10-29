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
            new("Nav.MyTasks", "/my/tasks", "list-checks", Permissions.Production.OwnTasks),
        ]),
        new("Nav.Section.Production",
        [
            new("Nav.Intake", "/workorders/intake", "scan-line", Permissions.WorkOrders.Create),
            new("Nav.Orders", "/orders", "shopping-bag", Permissions.Orders.Read),
            new("Nav.WorkOrders", "/workorders", "clipboard-list", Permissions.WorkOrders.Read),
            new("Nav.ProductionBoard", "/production/board", "square-kanban", Permissions.Production.Read),
            new("Nav.Queues", "/production/queue", "inbox", Permissions.Production.Read),
            new("Nav.Assign", "/production/assign", "user-check", Permissions.Production.Assign),
            new("Nav.Calendar", "/production/calendar", "calendar-days", Permissions.Production.Read),
        ]),
        new("Nav.Section.QualityDelivery",
        [
            new("Nav.Quality", "/quality/queue", "microscope", Permissions.Quality.Read),
            new("Nav.Adjustments", "/quality/adjustments", "wrench", Permissions.Quality.Read),
            new("Nav.Remakes", "/quality/remakes", "repeat", Permissions.Quality.Read),
            new("Nav.Delivery", "/delivery/ready", "package-check", Permissions.Delivery.Read),
            new("Nav.DeliveryNotes", "/delivery/notes", "clipboard-check", Permissions.Delivery.Read),
            new("Nav.DeliveryRuns", "/delivery/runs", "truck", Permissions.Delivery.Read),
        ]),
        new("Nav.Section.Parties",
        [
            new("Nav.Customers", "/customers", "stethoscope", Permissions.Customers.Read),
            new("Nav.Suppliers", "/suppliers", "building", Permissions.Suppliers.Read),
            new("Nav.Items", "/items", "tag", Permissions.Items.Read),
            new("Nav.PriceLists", "/price-lists", "receipt", Permissions.PriceLists.Read),
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
            new("Nav.Workflows", "/admin/workflows", "route", Permissions.Admin.WorkflowsManage),
            new("Nav.QualityTemplates", "/admin/quality-templates", "list-checks", Permissions.Admin.QualityManage),
            new("Nav.ReasonCodes", "/admin/reason-codes", "list", Permissions.Admin.ReasonsManage),
            new("Nav.Users", "/admin/users", "users", Permissions.Admin.UsersRead),
            new("Nav.Roles", "/admin/roles", "shield-check", Permissions.Admin.RolesManage),
            new("Nav.Numbering", "/admin/numbering", "hash", Permissions.Admin.NumberingManage),
            new("Nav.Units", "/admin/units", "ruler", Permissions.Admin.UnitsManage),
            new("Nav.Taxes", "/admin/taxes", "percent", Permissions.Admin.TaxesManage),
            new("Nav.Currencies", "/admin/currencies", "coins", Permissions.Admin.CurrenciesManage),
            new("Nav.Import", "/admin/import", "file-spreadsheet", Permissions.Admin.ImportManage),
            new("Nav.Audit", "/admin/audit", "scroll-text", Permissions.Admin.AuditRead),
            new("Nav.Subscription", "/admin/subscription", "sparkles", Permissions.Admin.SubscriptionRead),
        ]),
    ];

    public static bool IsActive(string currentPath, string href) =>
        href is "/admin" or "/platform"
            ? string.Equals(currentPath.TrimEnd('/'), href, StringComparison.OrdinalIgnoreCase)
            : currentPath.StartsWith(href, StringComparison.OrdinalIgnoreCase);
}
