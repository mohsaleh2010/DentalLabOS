using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Catalog;

public class ItemCategory : TenantEntity
{
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = default!;
    public ItemType? DefaultType { get; set; }
}

public class UnitOfMeasure : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int DecimalPlaces { get; set; } = 2;
}

/// <summary>Item-specific conversion: 1 <see cref="FromUnitId"/> = <see cref="Factor"/> × <see cref="ToUnitId"/>.</summary>
public class UnitConversion : TenantEntity
{
    public Guid ItemId { get; set; }
    public Guid FromUnitId { get; set; }
    public UnitOfMeasure FromUnit { get; set; } = default!;
    public Guid ToUnitId { get; set; }
    public UnitOfMeasure ToUnit { get; set; } = default!;
    public decimal Factor { get; set; }
}

public class Item : TenantEntity
{
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? NameEn { get; set; }
    public ItemType Type { get; set; }
    public Guid CategoryId { get; set; }
    public ItemCategory Category { get; set; } = default!;
    public Guid BaseUnitId { get; set; }
    public UnitOfMeasure BaseUnit { get; set; } = default!;

    /// <summary>True for materials, consumables and finished goods; false for services.</summary>
    public bool IsStockTracked { get; set; }
    public bool TracksBatches { get; set; }
    public bool TracksExpiry { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal StandardCost { get; set; }
    public decimal DefaultSalesPrice { get; set; }
    public Guid? TaxCodeId { get; set; }
    public Guid? SalesAccountId { get; set; }
    public Guid? InventoryAccountId { get; set; }
    public Guid? CogsAccountId { get; set; }

    /// <summary>Services: standard turnaround used to suggest a due date.</summary>
    public decimal StandardLeadHours { get; set; }
    public decimal DifficultyFactor { get; set; } = 1m;
    public Guid? DefaultWorkflowId { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<BomLine> BomLines { get; set; } = new List<BomLine>();

    public static bool IsStockType(ItemType type) => type is ItemType.Material or ItemType.Consumable or ItemType.FinishedGood;

    public static bool IsServiceType(ItemType type) => type is ItemType.Service or ItemType.OutsourcedService;
}

/// <summary>Standard consumption of a material by a service.</summary>
public class BomLine : TenantEntity
{
    public Guid ServiceItemId { get; set; }
    public Guid MaterialItemId { get; set; }
    public Item MaterialItem { get; set; } = default!;
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public UnitOfMeasure Unit { get; set; } = default!;
    public decimal ScrapPercent { get; set; }

    /// <summary>Quantity including scrap allowance.</summary>
    public decimal GrossQuantity => Quantity * (1 + (ScrapPercent / 100m));
}

public class PriceList : TenantEntity
{
    public string Name { get; set; } = default!;
    public string CurrencyCode { get; set; } = "EGP";

    /// <summary>The general list. At most one per lab.</summary>
    public bool IsDefault { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PriceListLine> Lines { get; set; } = new List<PriceListLine>();

    public bool IsValidOn(DateOnly date) =>
        IsActive && (ValidFrom is null || ValidFrom <= date) && (ValidTo is null || ValidTo >= date);
}

public class PriceListLine : TenantEntity
{
    public Guid PriceListId { get; set; }
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = default!;
    public decimal Price { get; set; }
    public decimal MinQty { get; set; } = 1;
}

public class CustomerItemPrice : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = default!;
    public decimal Price { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public bool IsValidOn(DateOnly date) => (ValidFrom is null || ValidFrom <= date) && (ValidTo is null || ValidTo >= date);
}

public class TaxCode : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Percentage, e.g. 14 for 14%. No default rates are seeded (needs accountant approval).</summary>
    public decimal Rate { get; set; }
    public bool IsWithholding { get; set; }
    public Guid? PayableAccountId { get; set; }
    public Guid? ReceivableAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Currency : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int DecimalPlaces { get; set; } = 2;
}

/// <summary>1 <see cref="FromCode"/> = <see cref="Rate"/> × <see cref="ToCode"/> on <see cref="Date"/>.</summary>
public class ExchangeRate : TenantEntity
{
    public string FromCode { get; set; } = default!;
    public string ToCode { get; set; } = default!;
    public DateOnly Date { get; set; }
    public decimal Rate { get; set; }
}
