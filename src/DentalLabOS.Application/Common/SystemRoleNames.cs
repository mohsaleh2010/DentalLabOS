using DentalLabOS.Domain.Security;

namespace DentalLabOS.Application.Common;

/// <summary>Display names given to built-in roles when a lab is provisioned (editable later).</summary>
public static class SystemRoleNames
{
    private static readonly Dictionary<string, (string Ar, string En)> Names = new()
    {
        [SystemRoles.Owner] = ("مالك المعمل", "Lab owner"),
        [SystemRoles.Manager] = ("مدير", "Manager"),
        [SystemRoles.Reception] = ("استقبال", "Reception"),
        [SystemRoles.ProductionManager] = ("مدير إنتاج", "Production manager"),
        [SystemRoles.DepartmentHead] = ("رئيس قسم", "Department head"),
        [SystemRoles.Technician] = ("فني", "Technician"),
        [SystemRoles.Quality] = ("جودة", "Quality"),
        [SystemRoles.Storekeeper] = ("مخزن", "Storekeeper"),
        [SystemRoles.Purchasing] = ("مشتريات", "Purchasing"),
        [SystemRoles.Sales] = ("مبيعات", "Sales"),
        [SystemRoles.Accountant] = ("محاسب", "Accountant"),
        [SystemRoles.Cashier] = ("أمين صندوق", "Cashier"),
        [SystemRoles.Delivery] = ("تسليم", "Delivery"),
    };

    public static string Get(string key, string culture) =>
        Names.TryGetValue(key, out var n) ? (culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? n.Ar : n.En) : key;
}

/// <summary>Default numbering sequences created for each new lab (ADM-11 can edit them).</summary>
public static class DefaultSequences
{
    public static readonly IReadOnlyList<(string Type, string Prefix)> All =
    [
        ("WorkOrder", "WO"), ("CustomerOrder", "ORD"), ("DeliveryNote", "DN"), ("SalesInvoice", "INV"),
        ("CreditNote", "CN"), ("Receipt", "RCV"), ("PaymentVoucher", "PV"), ("PurchaseOrder", "PO"),
        ("GoodsReceipt", "GR"), ("SupplierInvoice", "PINV"), ("StockDocument", "STK"), ("Expense", "EXP"),
        ("JournalEntry", "JE"),
    ];
}
