using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Common;

/// <summary>Numbering for master data: lab-wide, never reset yearly (C-00001, S-00001, ITM-00001).</summary>
public static class MasterDataSequences
{
    public const string Customer = "Customer";
    public const string Supplier = "Supplier";
    public const string Item = "Item";

    public static readonly IReadOnlyList<(string Type, string Prefix)> All = [(Customer, "C"), (Supplier, "S"), (Item, "ITM")];
}

/// <summary>
/// Starter reference data for a new lab (units, base currency, item categories, the general price list and master
/// data numbering). Everything is editable afterwards. No tax rates are seeded: they need the lab accountant's approval.
/// Must run inside the new tenant's scope so the write interceptor stamps the tenant.
/// </summary>
public static class TenantCatalogDefaults
{
    private static readonly (string Code, string Ar, string En, int Decimals)[] Units =
    [
        ("PCS", "قطعة", "Piece", 0), ("UNIT", "وحدة (سنّة)", "Unit (tooth)", 0), ("G", "جرام", "Gram", 2),
        ("ML", "مللي", "Millilitre", 2), ("BOX", "علبة", "Box", 0), ("DISC", "قرص", "Disc", 0), ("SET", "طقم", "Set", 0),
    ];

    private static readonly (string Ar, string En, ItemType Type)[] Categories =
    [
        ("تركيبات ثابتة", "Fixed prosthetics", ItemType.Service), ("تركيبات متحركة", "Removable prosthetics", ItemType.Service),
        ("زراعة", "Implants", ItemType.Service), ("تقويم", "Orthodontics", ItemType.Service),
        ("خامات", "Materials", ItemType.Material), ("مستهلكات", "Consumables", ItemType.Consumable),
    ];

    private static readonly Dictionary<string, (string Ar, string En)> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EGP"] = ("جنيه مصري", "Egyptian pound"), ["SAR"] = ("ريال سعودي", "Saudi riyal"), ["AED"] = ("درهم إماراتي", "UAE dirham"),
        ["KWD"] = ("دينار كويتي", "Kuwaiti dinar"), ["JOD"] = ("دينار أردني", "Jordanian dinar"), ["USD"] = ("دولار أمريكي", "US dollar"),
        ["EUR"] = ("يورو", "Euro"),
    };

    public static void Add(IApplicationDbContext db, string culture, string baseCurrency, int year)
    {
        var ar = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        string Name((string Ar, string En) n) => ar ? n.Ar : n.En;

        foreach (var u in Units)
        {
            db.Units.Add(new UnitOfMeasure { Code = u.Code, Name = Name((u.Ar, u.En)), DecimalPlaces = u.Decimals });
        }

        var code = baseCurrency.Trim().ToUpperInvariant();
        db.Currencies.Add(new Currency
        {
            Code = code,
            Name = CurrencyNames.TryGetValue(code, out var cn) ? Name(cn) : code,
            DecimalPlaces = code is "KWD" or "JOD" ? 3 : 2,
        });

        foreach (var c in Categories)
        {
            db.ItemCategories.Add(new ItemCategory { Name = Name((c.Ar, c.En)), DefaultType = c.Type });
        }

        db.PriceLists.Add(new PriceList { Name = ar ? "قائمة الأسعار العامة" : "General price list", CurrencyCode = code, IsDefault = true });

        foreach (var (type, prefix) in MasterDataSequences.All)
        {
            db.DocumentSequences.Add(NewMasterSequence(type, prefix, year));
        }
    }

    /// <summary>Adds the defaults to an existing lab that predates them (idempotent; call inside the tenant's scope).</summary>
    public static async Task<bool> BackfillAsync(IApplicationDbContext db, string culture, string baseCurrency, int year, CancellationToken ct)
    {
        var changed = false;
        if (!await db.Units.AnyAsync(ct))
        {
            Add(db, culture, baseCurrency, year);
            return true;
        }

        var existing = await db.DocumentSequences.Where(s => s.BranchId == null).Select(s => s.DocumentType).ToListAsync(ct);
        foreach (var (type, prefix) in MasterDataSequences.All.Where(s => !existing.Contains(s.Type)))
        {
            db.DocumentSequences.Add(NewMasterSequence(type, prefix, year));
            changed = true;
        }

        return changed;
    }

    private static DocumentSequence NewMasterSequence(string type, string prefix, int year) =>
        new() { DocumentType = type, Prefix = prefix, Year = year, Padding = 5, ResetYearly = false };
}
