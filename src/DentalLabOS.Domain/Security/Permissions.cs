namespace DentalLabOS.Domain.Security;

/// <summary>
/// The permission catalog (plan A3.1/A4). Keys follow <c>module.entity.action</c> in lower case.
/// The whole catalog is declared up-front so roles and screens are stable; screens of later phases
/// reference keys that already exist here. Constants are provided for keys used in code.
/// </summary>
public static class Permissions
{
    public static class Platform
    {
        public const string DashboardRead = "platform.dashboard.read";
        public const string TenantsRead = "platform.tenants.read";
        public const string TenantsCreate = "platform.tenants.create";
        public const string TenantsManage = "platform.tenants.manage";
        public const string PlansManage = "platform.plans.manage";
        public const string BillingRead = "platform.billing.read";
        public const string SupportManage = "platform.support.manage";
        public const string HealthRead = "platform.health.read";
        public const string FeaturesManage = "platform.features.manage";
        public const string UsersManage = "platform.users.manage";
        public const string AuditRead = "platform.audit.read";
    }

    public static class Admin
    {
        public const string DashboardRead = "admin.dashboard.read";
        public const string CompanyManage = "admin.company.manage";
        public const string BranchesManage = "admin.branches.manage";
        public const string DepartmentsManage = "admin.departments.manage";
        public const string UsersRead = "admin.users.read";
        public const string UsersManage = "admin.users.manage";
        public const string TechniciansManage = "admin.technicians.manage";
        public const string RolesManage = "admin.roles.manage";
        public const string NumberingManage = "admin.numbering.manage";
        public const string AuditRead = "admin.audit.read";
        public const string SubscriptionRead = "admin.subscription.read";
        public const string ApprovalsManage = "admin.approvals.manage";
    }

    /// <summary>Every tenant-level permission key (platform keys are in <see cref="PlatformKeys"/>).</summary>
    public static readonly IReadOnlyList<string> TenantKeys =
    [
        // ADM — lab administration
        "admin.dashboard.read", "admin.company.manage", "admin.branches.manage", "admin.departments.manage",
        "admin.users.read", "admin.users.manage", "admin.technicians.manage", "admin.roles.manage",
        "admin.numbering.manage", "admin.taxes.manage", "admin.currencies.manage", "admin.units.manage",
        "admin.workflows.manage", "admin.quality.manage", "admin.reasons.manage", "admin.costing.manage",
        "admin.approvals.manage", "admin.notifications.manage", "admin.ai.manage", "admin.import.manage",
        "admin.export.manage", "admin.audit.read", "admin.subscription.read", "admin.privacy.manage",
        // CUS — customers (clinics & dentists)
        "customers.read", "customers.manage", "customers.dentists.manage", "customers.groups.manage",
        "customers.statement.read", "customers.credit.manage", "customers.portal.manage", "customers.merge",
        // SUP — suppliers
        "suppliers.read", "suppliers.manage", "suppliers.statement.read", "suppliers.items.manage", "suppliers.rating.manage",
        // ITM — items & prices
        "items.read", "items.manage", "items.categories.manage", "items.services.read", "items.bom.manage",
        "items.labels.print", "items.import", "pricelists.read", "pricelists.manage", "pricelists.bulk",
        // ORD — orders & work orders & production & quality & delivery
        "orders.read", "orders.create", "workorders.read", "workorders.create", "workorders.update",
        "workorders.hold", "workorders.transition", "workorders.cancel", "workorders.print", "workorders.cost.read",
        "production.read", "production.assign", "tasks.own.read",
        "quality.read", "quality.inspect", "quality.remake.manage",
        "delivery.read", "delivery.manage", "delivery.confirm",
        // INV — inventory
        "inventory.read", "inventory.warehouses.manage", "inventory.receipt.create", "inventory.issue.create",
        "inventory.issue.workorder", "inventory.issue.post", "inventory.return.workorder", "inventory.transfer.create",
        "inventory.adjust.create", "inventory.count.manage", "inventory.count.enter", "inventory.batches.read",
        "inventory.waste.create", "inventory.valuation.read", "inventory.requisition.create", "inventory.approve",
        // PUR — purchasing
        "purchasing.request.read", "purchasing.order.read", "purchasing.order.manage", "purchasing.receipt.read",
        "purchasing.receipt.manage", "purchasing.invoice.read", "purchasing.invoice.manage", "purchasing.return.manage",
        "purchasing.payables.read",
        // SAL — sales & invoicing
        "sales.quote.read", "sales.quote.manage", "sales.invoice.read", "sales.invoice.create", "sales.invoice.batch",
        "sales.invoice.void", "sales.creditnote.manage", "sales.approve", "sales.receivables.read", "sales.reports.read",
        // PAY — payments
        "payments.read", "payments.in.read", "payments.in.create", "payments.out.read", "payments.out.create",
        "payments.allocate", "payments.cheques.read", "payments.cheques.manage", "payments.advances.read", "payments.reverse",
        // TRS — treasury & banks
        "treasury.read", "treasury.manage", "treasury.statement.read", "treasury.transfer.create", "treasury.shift.open",
        "treasury.shift.close", "treasury.daily.read", "treasury.reconcile", "treasury.misc.create",
        // EXP — expenses
        "expenses.read", "expenses.create", "expenses.categories.manage", "expenses.recurring.manage",
        "expenses.approve", "expenses.reports.read",
        // ACC — accounting
        "accounting.accounts.read", "accounting.accounts.manage", "accounting.journal.read", "accounting.journal.create",
        "accounting.journal.post", "accounting.post", "accounting.mappings.manage", "accounting.periods.manage",
        "accounting.costcenters.manage", "accounting.opening.manage", "accounting.reports.read", "accounting.tax.read",
        "accounting.close", "accounting.reverse",
        // RPT — reports
        "reports.read", "reports.operations.read", "reports.quality.read", "reports.productivity.read",
        "reports.profit.read", "reports.inventory.read", "reports.purchasing.read", "reports.sales.read",
        // AI
        "ai.assistant.use", "ai.suggestions.review", "ai.usage.read",
    ];

    /// <summary>Dentist/clinic portal keys (DOC module), granted to portal accounts only.</summary>
    public static readonly IReadOnlyList<string> PortalKeys =
    [
        "portal.read", "portal.workorders.read", "portal.workorders.create", "portal.requests.create",
        "portal.invoices.read", "portal.payments.read", "portal.messages.manage", "portal.clinic.manage",
    ];

    public static readonly IReadOnlyList<string> PlatformKeys =
    [
        Platform.DashboardRead, Platform.TenantsRead, Platform.TenantsCreate, Platform.TenantsManage,
        Platform.PlansManage, Platform.BillingRead, Platform.SupportManage, Platform.HealthRead,
        Platform.FeaturesManage, Platform.UsersManage, Platform.AuditRead,
    ];

    public static IEnumerable<string> All => TenantKeys.Concat(PortalKeys).Concat(PlatformKeys);

    public static string ModuleOf(string key) => key[..key.IndexOf('.')];

    /// <summary>Permissions held by each platform staff role.</summary>
    public static IReadOnlyCollection<string> ForPlatformRole(Common.PlatformRole role) => role switch
    {
        Common.PlatformRole.Owner => PlatformKeys.ToArray(),
        Common.PlatformRole.Support =>
        [
            Platform.DashboardRead, Platform.TenantsRead, Platform.HealthRead, Platform.SupportManage,
        ],
        _ => [],
    };
}
