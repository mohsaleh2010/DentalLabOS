namespace DentalLabOS.Domain.Security;

/// <summary>
/// Built-in lab roles and their default permissions, derived from the role matrix in plan A3.2.
/// "●" (full) grants every key of the module; "◐" (partial) grants the listed subset.
/// Tenants can adjust non-owner roles later from ADM-09.
/// </summary>
public static class SystemRoles
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Reception = "reception";
    public const string ProductionManager = "production_manager";
    public const string DepartmentHead = "department_head";
    public const string Technician = "technician";
    public const string Quality = "quality";
    public const string Storekeeper = "storekeeper";
    public const string Purchasing = "purchasing";
    public const string Sales = "sales";
    public const string Accountant = "accountant";
    public const string Cashier = "cashier";
    public const string Delivery = "delivery";

    public sealed record Definition(string Key, string NameKey, IReadOnlyCollection<string> Permissions);

    private static readonly string[] Ord = ["orders.", "workorders.", "production.", "tasks.", "delivery."];
    private static readonly string[] Qc = ["quality."];

    private static IEnumerable<string> Modules(params string[] prefixes) =>
        Security.Permissions.TenantKeys.Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)));

    private static IEnumerable<string> Reads(params string[] prefixes) =>
        Modules(prefixes).Where(k => k.EndsWith(".read", StringComparison.Ordinal));

    public static readonly IReadOnlyList<Definition> All =
    [
        new(Owner, "Roles.Owner", Security.Permissions.TenantKeys.ToArray()),
        new(Manager, "Roles.Manager", Security.Permissions.TenantKeys
            .Where(k => !k.StartsWith("admin.roles", StringComparison.Ordinal)
                        && !k.StartsWith("admin.privacy", StringComparison.Ordinal)
                        && !(k.StartsWith("accounting.", StringComparison.Ordinal) && !k.EndsWith(".read", StringComparison.Ordinal)))
            .ToArray()),
        new(Reception, "Roles.Reception", Modules(Ord).Where(k => !k.Contains(".cost.", StringComparison.Ordinal))
            .Concat(["customers.read", "customers.dentists.manage", "items.read", "items.services.read", "pricelists.read",
                     "sales.invoice.read", "reports.read", "reports.operations.read"]).ToArray()),
        new(ProductionManager, "Roles.ProductionManager", Modules([.. Ord, .. Qc]).Concat(Reads("customers.", "items.", "inventory."))
            .Concat(["admin.dashboard.read", "inventory.requisition.create", "reports.read", "reports.operations.read",
                     "reports.quality.read", "reports.productivity.read"]).ToArray()),
        new(DepartmentHead, "Roles.DepartmentHead",
            ["workorders.read", "workorders.transition", "workorders.hold", "production.read", "production.assign", "tasks.own.read",
             "quality.read", "items.read", "items.services.read", "inventory.requisition.create", "reports.read", "reports.productivity.read"]),
        new(Technician, "Roles.Technician",
            ["tasks.own.read", "workorders.read", "workorders.transition", "production.read", "inventory.requisition.create"]),
        new(Quality, "Roles.Quality", Modules(Qc).Concat(["workorders.read", "workorders.hold", "production.read", "reports.read", "reports.quality.read"]).ToArray()),
        new(Storekeeper, "Roles.Storekeeper", Modules("inventory.", "items.").Concat(["suppliers.read", "purchasing.receipt.read",
            "purchasing.receipt.manage", "purchasing.order.read", "reports.read", "reports.inventory.read"]).ToArray()),
        new(Purchasing, "Roles.Purchasing", Modules("purchasing.", "suppliers.").Concat(Reads("items.", "inventory."))
            .Concat(["expenses.read", "reports.read", "reports.purchasing.read"]).ToArray()),
        new(Sales, "Roles.Sales", Modules("sales.", "customers.").Concat(Reads("items.", "orders.", "workorders."))
            .Concat(["reports.read", "reports.sales.read"]).ToArray()),
        new(Accountant, "Roles.Accountant", Modules("customers.", "suppliers.", "items.", "pricelists.", "inventory.", "purchasing.",
                "sales.", "payments.", "treasury.", "expenses.", "accounting.", "reports.")
            .Concat(["workorders.read", "workorders.cost.read", "quality.read", "admin.costing.manage", "admin.taxes.manage",
                     "admin.currencies.manage"]).ToArray()),
        new(Cashier, "Roles.Cashier", ["customers.read", "suppliers.read", "payments.read", "payments.in.read", "payments.in.create",
            "payments.out.read", "payments.out.create", "payments.cheques.read", "treasury.read", "treasury.shift.open",
            "treasury.shift.close", "treasury.daily.read", "expenses.read", "expenses.create", "reports.read"]),
        new(Delivery, "Roles.Delivery", ["delivery.read", "delivery.manage", "delivery.confirm", "workorders.read", "customers.read"]),
    ];
}
