using System.Globalization;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Catalog;

/// <summary>
/// Excel import of customers and items (ADM-23, ITM-11). Two steps: preview validates every row and saves nothing;
/// import re-validates the same file and saves all rows in one transaction, or none if any row is invalid.
/// Headers are matched in Arabic or English, so a template downloaded in either language works.
/// </summary>
public sealed class ImportService(
    IApplicationDbContext db,
    IPermissionService permissions,
    INumberingService numbering,
    ISpreadsheetService spreadsheets,
    IAuditWriter audit,
    IClock clock)
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 5 * 1024 * 1024;
    private const int SampleRows = 10;

    private sealed record Column(string Key, string Ar, string En, bool Required, string HelpAr, string HelpEn);

    private static readonly Column[] CustomerColumns =
    [
        new("Code", "الكود", "Code", false, "اتركه فارغًا ليُرقَّم تلقائيًا", "Leave empty for automatic numbering"),
        new("Name", "الاسم", "Name", true, "اسم العيادة أو الطبيب", "Clinic or dentist name"),
        new("Type", "النوع", "Type", false, "عيادة / طبيب / مركز / فرد (الافتراضي: عيادة)", "Clinic / Dentist / Center / Individual (default: Clinic)"),
        new("Group", "المجموعة", "Group", false, "تُنشأ إن لم تكن موجودة", "Created if it doesn't exist"),
        new("Phone", "الهاتف", "Phone", false, "", ""),
        new("Mobile", "الموبايل", "Mobile", false, "", ""),
        new("Email", "البريد الإلكتروني", "Email", false, "", ""),
        new("City", "المدينة", "City", false, "", ""),
        new("Address", "العنوان", "Address", false, "", ""),
        new("TaxNumber", "الرقم الضريبي", "Tax number", false, "", ""),
        new("PaymentTermsDays", "مدة السداد (يوم)", "Payment terms (days)", false, "0 إلى 365", "0 to 365"),
        new("CreditLimit", "الحد الائتماني", "Credit limit", false, "0 = بلا حد. يتطلب صلاحية الائتمان", "0 = no limit. Needs the credit permission"),
        new("OpeningBalance", "الرصيد الافتتاحي", "Opening balance", false, "موجب = مستحق على العميل", "Positive = owed by the customer"),
        new("Dentist", "اسم الطبيب", "Dentist", false, "يُضاف طبيبًا لهذا العميل", "Added as a dentist of this customer"),
    ];

    private static readonly Column[] ItemColumns =
    [
        new("Sku", "الكود", "SKU", false, "اتركه فارغًا ليُرقَّم تلقائيًا", "Leave empty for automatic numbering"),
        new("Name", "الاسم", "Name", true, "", ""),
        new("NameEn", "الاسم بالإنجليزية", "English name", false, "", ""),
        new("Type", "النوع", "Type", true, "خدمة / خامة / مستهلك / منتج تام / خدمة خارجية", "Service / Material / Consumable / FinishedGood / OutsourcedService"),
        new("Category", "التصنيف", "Category", true, "يُنشأ إن لم يكن موجودًا", "Created if it doesn't exist"),
        new("Unit", "الوحدة", "Unit", true, "كود أو اسم وحدة موجودة (مثل PCS أو قطعة)", "Code or name of an existing unit (e.g. PCS)"),
        new("SalesPrice", "سعر البيع", "Sales price", false, "", ""),
        new("StandardCost", "التكلفة المعيارية", "Standard cost", false, "يُتجاهل دون صلاحية رؤية التكلفة", "Ignored without the cost permission"),
        new("ReorderLevel", "حد إعادة الطلب", "Reorder level", false, "للخامات والمستهلكات فقط", "Materials and consumables only"),
        new("LeadHours", "مدة التنفيذ (ساعة)", "Lead time (hours)", false, "للخدمات فقط", "Services only"),
        new("Barcode", "الباركود", "Barcode", false, "", ""),
    ];

    private static readonly Dictionary<string, PartyType> PartyTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["عيادة"] = PartyType.Clinic, ["طبيب"] = PartyType.Dentist, ["مركز"] = PartyType.Center, ["فرد"] = PartyType.Individual,
    };

    private static readonly Dictionary<string, ItemType> ItemTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["خدمة"] = ItemType.Service, ["خامة"] = ItemType.Material, ["مستهلك"] = ItemType.Consumable, ["مستهلكات"] = ItemType.Consumable,
        ["منتج تام"] = ItemType.FinishedGood, ["خدمة خارجية"] = ItemType.OutsourcedService, ["Finished good"] = ItemType.FinishedGood,
        ["Outsourced service"] = ItemType.OutsourcedService,
    };

    public async Task<byte[]> TemplateAsync(ImportKind kind, string culture, CancellationToken ct = default)
    {
        await DemandAsync(kind, ct);
        var ar = culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var columns = ColumnsOf(kind);
        return spreadsheets.CreateTemplate(
            kind == ImportKind.Customers ? (ar ? "العملاء" : "Customers") : (ar ? "الأصناف" : "Items"),
            columns.Select(c => (ar ? c.Ar : c.En) + (c.Required ? " *" : string.Empty)).ToList(),
            ar ? "تعليمات" : "Help",
            columns.Select(c => (ar ? c.Ar : c.En, ar ? c.HelpAr : c.HelpEn)).ToList());
    }

    public async Task<ImportPreviewDto> PreviewAsync(ImportKind kind, Stream file, CancellationToken ct = default)
    {
        await DemandAsync(kind, ct);
        var plan = await PlanAsync(kind, file, ct);
        return plan.Preview;
    }

    public async Task<ImportResultDto> ImportAsync(ImportKind kind, Stream file, CancellationToken ct = default)
    {
        await DemandAsync(kind, ct);
        var plan = await PlanAsync(kind, file, ct);
        if (!plan.Preview.CanImport)
        {
            throw ValidationException.For("File", plan.Preview.TotalRows == 0 ? "Import.Empty" : "Import.HasErrors");
        }

        var created = await db.InTransactionAsync(async innerCt =>
        {
            var count = kind == ImportKind.Customers
                ? await SaveCustomersAsync(plan.Rows, innerCt)
                : await SaveItemsAsync(plan.Rows, plan.CanSetCost, innerCt);
            audit.Add(AuditActions.Create, kind == ImportKind.Customers ? nameof(Customer) : nameof(Item), null, "Import", new { rows = count });
            await db.SaveChangesAsync(innerCt);
            return count;
        }, ct);

        return new ImportResultDto(kind.ToString(), created);
    }

    private async Task DemandAsync(ImportKind kind, CancellationToken ct)
    {
        if (kind == ImportKind.Customers)
        {
            await permissions.DemandAsync(Permissions.Admin.ImportManage, ct);
            await permissions.DemandAsync(Permissions.Customers.Manage, ct);
        }
        else
        {
            await permissions.DemandAsync(Permissions.Items.Import, ct);
        }
    }

    /// <summary>The column's header text in the given culture (for showing errors and previews).</summary>
    public static string HeaderOf(ImportKind kind, string key, string culture)
    {
        var column = ColumnsOf(kind).FirstOrDefault(c => c.Key == key);
        return column is null ? key : culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? column.Ar : column.En;
    }

    private static Column[] ColumnsOf(ImportKind kind) => kind == ImportKind.Customers ? CustomerColumns : ItemColumns;

    private sealed record Row(int Number, IReadOnlyDictionary<string, string?> Values)
    {
        public string? this[string key] => Values.TryGetValue(key, out var v) ? MasterData.Clean(v) : null;
    }

    private sealed record Plan(ImportPreviewDto Preview, IReadOnlyList<Row> Rows, bool CanSetCost);

    private async Task<Plan> PlanAsync(ImportKind kind, Stream file, CancellationToken ct)
    {
        var sheet = spreadsheets.Read(file, MaxRows);
        var columns = ColumnsOf(kind);
        var errors = new List<ImportRowError>();

        // Map each sheet column to a known key by its Arabic/English header (a trailing * is ignored).
        var map = new Dictionary<int, Column>();
        for (var i = 0; i < sheet.Headers.Count; i++)
        {
            var header = sheet.Headers[i].Trim().TrimEnd('*').Trim();
            var col = columns.FirstOrDefault(c => string.Equals(header, c.Ar, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(header, c.En, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(header, c.Key, StringComparison.OrdinalIgnoreCase));
            if (col is not null && !map.ContainsValue(col))
            {
                map[i] = col;
            }
        }

        foreach (var missing in columns.Where(c => c.Required && !map.ContainsValue(c)))
        {
            errors.Add(new ImportRowError(1, missing.Key, "Import.MissingColumn"));
        }

        var rows = sheet.Rows
            .Select((cells, index) => new Row(index + 2, map.ToDictionary(m => m.Value.Key, m => m.Key < cells.Count ? cells[m.Key] : null)))
            .Where(r => r.Values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            .ToList();

        var canSetCost = await permissions.HasAsync(Permissions.Items.CostRead, ct);
        if (errors.Count == 0)
        {
            if (kind == ImportKind.Customers)
            {
                await ValidateCustomersAsync(rows, errors, ct);
            }
            else
            {
                await ValidateItemsAsync(rows, errors, ct);
            }
        }

        var ordered = map.OrderBy(m => Array.IndexOf(columns, m.Value)).Select(m => m.Value).ToList();
        var sample = rows.Take(SampleRows).Select(r => (IReadOnlyList<string?>)ordered.Select(c => r[c.Key]).ToList()).ToList();
        var invalidRows = errors.Where(e => e.Row > 1).Select(e => e.Row).Distinct().Count();
        var preview = new ImportPreviewDto(kind.ToString(), rows.Count, rows.Count - invalidRows, ordered.Select(c => c.Key).ToList(), sample,
            errors.OrderBy(e => e.Row).Take(200).ToList());
        return new Plan(preview, rows, canSetCost);
    }

    // ---- Customers ----

    private async Task ValidateCustomersAsync(List<Row> rows, List<ImportRowError> errors, CancellationToken ct)
    {
        var existingCodes = (await db.Customers.AsNoTracking().Select(c => c.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canSetCredit = await permissions.HasAsync(Permissions.Customers.CreditManage, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            void Err(string col, string key) => errors.Add(new ImportRowError(r.Number, col, key));

            var code = MasterData.NormalizeCode(r["Code"]);
            if (code is not null)
            {
                if (code.Length > 20 || !System.Text.RegularExpressions.Regex.IsMatch(code, MasterData.CodePattern))
                {
                    Err("Code", "Validation.Code");
                }
                else if (existingCodes.Contains(code) || !seen.Add(code))
                {
                    Err("Code", "Validation.CodeTaken");
                }
            }

            Required(r, "Name", 200, Err);
            if (r["Type"] is { } type && ParsePartyType(type) is null)
            {
                Err("Type", "Import.InvalidValue");
            }

            MaxLen(r, "Group", 150, Err);
            MaxLen(r, "Phone", 50, Err);
            MaxLen(r, "Mobile", 50, Err);
            MaxLen(r, "City", 100, Err);
            MaxLen(r, "Address", 500, Err);
            MaxLen(r, "TaxNumber", 50, Err);
            MaxLen(r, "Dentist", 150, Err);
            if (r["Email"] is { } email && !System.Net.Mail.MailAddress.TryCreate(email, out _))
            {
                Err("Email", "Validation.Email");
            }

            Number(r, "PaymentTermsDays", 0, 365, Err);
            var credit = Number(r, "CreditLimit", 0, 1_000_000_000m, Err);
            if (credit is > 0 && !canSetCredit)
            {
                Err("CreditLimit", "Validation.CreditPermission");
            }

            Number(r, "OpeningBalance", -1_000_000_000m, 1_000_000_000m, Err);
        }
    }

    private async Task<int> SaveCustomersAsync(IReadOnlyList<Row> rows, CancellationToken ct)
    {
        var groups = await db.CustomerGroups.ToDictionaryAsync(g => g.Name, StringComparer.OrdinalIgnoreCase, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var codes = (await db.Customers.AsNoTracking().Select(c => c.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (MasterData.NormalizeCode(r["Code"]) is { } manual)
            {
                codes.Add(manual);
            }
        }

        foreach (var r in rows)
        {
            CustomerGroup? group = null;
            if (r["Group"] is { } groupName && !groups.TryGetValue(groupName, out group))
            {
                group = db.CustomerGroups.Add(new CustomerGroup { Name = groupName }).Entity;
                groups[groupName] = group;
            }

            var code = MasterData.NormalizeCode(r["Code"])
                       ?? await MasterData.NextCodeAsync(numbering, MasterDataSequences.Customer, today, c => Task.FromResult(codes.Contains(c)), ct);
            codes.Add(code);

            var customer = new Customer
            {
                Code = code,
                Name = r["Name"]!,
                Type = r["Type"] is { } t ? ParsePartyType(t)!.Value : PartyType.Clinic,
                GroupId = group?.Id,
                Phone = r["Phone"],
                Mobile = r["Mobile"],
                Email = r["Email"],
                City = r["City"],
                Address = r["Address"],
                TaxNumber = r["TaxNumber"],
                PaymentTermsDays = (int)(ParseNumber(r["PaymentTermsDays"]) ?? 0),
                CreditLimit = ParseNumber(r["CreditLimit"]) ?? 0,
                OpeningBalance = ParseNumber(r["OpeningBalance"]) ?? 0,
            };
            db.Customers.Add(customer);
            if (r["Dentist"] is { } dentist)
            {
                db.Dentists.Add(new Dentist { CustomerId = customer.Id, FullName = dentist });
            }
        }

        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    // ---- Items ----

    private async Task ValidateItemsAsync(List<Row> rows, List<ImportRowError> errors, CancellationToken ct)
    {
        var existingSkus = (await db.Items.AsNoTracking().Select(i => i.Sku).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingBarcodes = (await db.Items.AsNoTracking().Where(i => i.Barcode != null).Select(i => i.Barcode!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var units = await UnitLookupAsync(ct);
        var seenSku = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBarcode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            void Err(string col, string key) => errors.Add(new ImportRowError(r.Number, col, key));

            var sku = MasterData.NormalizeCode(r["Sku"]);
            if (sku is not null)
            {
                if (sku.Length > 30 || !System.Text.RegularExpressions.Regex.IsMatch(sku, MasterData.CodePattern))
                {
                    Err("Sku", "Validation.Code");
                }
                else if (existingSkus.Contains(sku) || !seenSku.Add(sku))
                {
                    Err("Sku", "Validation.CodeTaken");
                }
            }

            Required(r, "Name", 200, Err);
            MaxLen(r, "NameEn", 200, Err);
            Required(r, "Category", 150, Err);
            if (r["Type"] is not { } type)
            {
                Err("Type", "Validation.Required");
            }
            else if (ParseItemType(type) is null)
            {
                Err("Type", "Import.InvalidValue");
            }

            if (r["Unit"] is not { } unit)
            {
                Err("Unit", "Validation.Required");
            }
            else if (!units.ContainsKey(unit))
            {
                Err("Unit", "Import.UnitNotFound");
            }

            Number(r, "SalesPrice", 0, 100_000_000m, Err);
            Number(r, "StandardCost", 0, 100_000_000m, Err);
            Number(r, "ReorderLevel", 0, 10_000_000m, Err);
            Number(r, "LeadHours", 0, 2000, Err);
            if (r["Barcode"] is { } barcode)
            {
                if (barcode.Length > 50)
                {
                    Err("Barcode", "Validation.TooLong");
                }
                else if (existingBarcodes.Contains(barcode) || !seenBarcode.Add(barcode))
                {
                    Err("Barcode", "Validation.BarcodeTaken");
                }
            }
        }
    }

    private async Task<int> SaveItemsAsync(IReadOnlyList<Row> rows, bool canSetCost, CancellationToken ct)
    {
        var units = await UnitLookupAsync(ct);
        var categories = (await db.ItemCategories.Where(c => c.ParentId == null).ToListAsync(ct))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var skus = (await db.Items.AsNoTracking().Select(i => i.Sku).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (MasterData.NormalizeCode(r["Sku"]) is { } manual)
            {
                skus.Add(manual);
            }
        }

        foreach (var r in rows)
        {
            var type = ParseItemType(r["Type"]!)!.Value;
            var categoryName = r["Category"]!;
            if (!categories.TryGetValue(categoryName, out var category))
            {
                category = db.ItemCategories.Add(new ItemCategory { Name = categoryName, DefaultType = type }).Entity;
                categories[categoryName] = category;
            }

            var sku = MasterData.NormalizeCode(r["Sku"])
                      ?? await MasterData.NextCodeAsync(numbering, MasterDataSequences.Item, today, c => Task.FromResult(skus.Contains(c)), ct);
            skus.Add(sku);

            var stock = Item.IsStockType(type);
            db.Items.Add(new Item
            {
                Sku = sku,
                Name = r["Name"]!,
                NameEn = r["NameEn"],
                Type = type,
                CategoryId = category.Id,
                BaseUnitId = units[r["Unit"]!],
                IsStockTracked = stock,
                DefaultSalesPrice = ParseNumber(r["SalesPrice"]) ?? 0,
                StandardCost = canSetCost ? ParseNumber(r["StandardCost"]) ?? 0 : 0,
                ReorderLevel = stock ? ParseNumber(r["ReorderLevel"]) ?? 0 : 0,
                StandardLeadHours = Item.IsServiceType(type) ? ParseNumber(r["LeadHours"]) ?? 0 : 0,
                Barcode = r["Barcode"],
            });
        }

        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    /// <summary>Units by code and by name (case-insensitive).</summary>
    private async Task<Dictionary<string, Guid>> UnitLookupAsync(CancellationToken ct)
    {
        var units = await db.Units.AsNoTracking().Select(u => new { u.Id, u.Code, u.Name }).ToListAsync(ct);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in units)
        {
            map.TryAdd(u.Code, u.Id);
        }

        foreach (var u in units)
        {
            map.TryAdd(u.Name, u.Id);
        }

        return map;
    }

    // ---- Parsing helpers ----

    private static void Required(Row r, string key, int max, Action<string, string> err)
    {
        if (r[key] is null)
        {
            err(key, "Validation.Required");
        }
        else
        {
            MaxLen(r, key, max, err);
        }
    }

    private static void MaxLen(Row r, string key, int max, Action<string, string> err)
    {
        if (r[key] is { } v && v.Length > max)
        {
            err(key, "Validation.TooLong");
        }
    }

    private static decimal? Number(Row r, string key, decimal min, decimal max, Action<string, string> err)
    {
        if (r[key] is not { } raw)
        {
            return null;
        }

        var value = ParseNumber(raw);
        if (value is null)
        {
            err(key, "Import.InvalidNumber");
        }
        else if (value < min || value > max)
        {
            err(key, "Validation.Range");
        }

        return value;
    }

    /// <summary>Invariant numbers; Arabic-Indic digits and separators are accepted too.</summary>
    public static decimal? ParseNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var chars = raw.Trim().Select(ch => ch switch
        {
            >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
            >= '۰' and <= '۹' => (char)('0' + (ch - '۰')),
            '٫' => '.',
            _ => ch,
        }).Where(ch => ch is not ('٬' or ',' or ' ')).ToArray();

        return decimal.TryParse(new string(chars), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static PartyType? ParsePartyType(string value) =>
        PartyTypeAliases.TryGetValue(value.Trim(), out var t) ? t : MasterData.TryParseEnum<PartyType>(value, out var e) ? e : null;

    public static ItemType? ParseItemType(string value) =>
        ItemTypeAliases.TryGetValue(value.Trim(), out var t) ? t : MasterData.TryParseEnum<ItemType>(value.Replace(" ", string.Empty), out var e) ? e : null;
}
