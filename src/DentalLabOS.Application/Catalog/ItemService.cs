using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Catalog;

public sealed record ItemFilter(string? Type = null, Guid? CategoryId = null, bool? Active = null, bool ServicesOnly = false, bool StockOnly = false);

/// <summary>
/// Items and services (ITM-01..06), their categories, unit conversions and standard material consumption (BOM).
/// Standard cost is visible only with <see cref="Permissions.Items.CostRead"/>.
/// </summary>
public sealed class ItemService(IApplicationDbContext db, IPermissionService permissions, INumberingService numbering, IClock clock)
{
    public async Task<PagedResult<ItemListItemDto>> ListAsync(PageQuery page, ItemFilter? filter = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(filter?.ServicesOnly == true ? Permissions.Items.ServicesRead : Permissions.Items.Read, ct);
        var showCost = await permissions.HasAsync(Permissions.Items.CostRead, ct);

        var query = Filter(db.Items.AsNoTracking(), page.NormalizedSearch(), filter);
        query = page.Sort switch
        {
            "sku" => query.OrderBy(i => i.Sku),
            "price" => query.OrderByDescending(i => i.DefaultSalesPrice),
            _ => query.OrderBy(i => i.Name),
        };

        return await query
            .Select(i => new ItemListItemDto(i.Id, i.Sku, i.Name, i.NameEn, i.Type.ToString(), i.Category.Name, i.BaseUnit.Code, i.DefaultSalesPrice,
                showCost ? i.StandardCost : null, i.IsStockTracked, i.IsActive))
            .ToPagedAsync(page, ct);
    }

    /// <summary>Active items for pickers. <paramref name="kind"/>: "service", "stock" or null for all.</summary>
    public async Task<IReadOnlyList<OptionDto>> OptionsAsync(string? search, string? kind = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        var filter = new ItemFilter(Active: true, ServicesOnly: kind == "service", StockOnly: kind == "stock");
        return await Filter(db.Items.AsNoTracking(), MasterData.Clean(search), filter)
            .OrderBy(i => i.Name).Take(50).Select(i => new OptionDto(i.Id, i.Sku, i.Name)).ToListAsync(ct);
    }

    private static IQueryable<Item> Filter(IQueryable<Item> query, string? search, ItemFilter? filter)
    {
        if (search is not null)
        {
            query = query.Where(i => i.Name.Contains(search) || i.Sku.Contains(search) || (i.NameEn != null && i.NameEn.Contains(search))
                                     || i.Barcode == search);
        }

        if (MasterData.TryParseEnum<ItemType>(filter?.Type, out var type))
        {
            query = query.Where(i => i.Type == type);
        }

        if (filter?.CategoryId is { } categoryId)
        {
            query = query.Where(i => i.CategoryId == categoryId);
        }

        if (filter?.Active is { } active)
        {
            query = query.Where(i => i.IsActive == active);
        }

        if (filter?.ServicesOnly == true)
        {
            query = query.Where(i => i.Type == ItemType.Service || i.Type == ItemType.OutsourcedService);
        }

        if (filter?.StockOnly == true)
        {
            query = query.Where(i => i.IsStockTracked);
        }

        return query;
    }

    public async Task<ItemDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        var showCost = await permissions.HasAsync(Permissions.Items.CostRead, ct);
        return await db.Items.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new ItemDto(i.Id, i.Sku, i.Name, i.NameEn, i.Type.ToString(), i.CategoryId, i.Category.Name, i.BaseUnitId, i.BaseUnit.Code,
                i.IsStockTracked, i.TracksBatches, i.TracksExpiry, i.ReorderLevel, i.ReorderQty, showCost ? i.StandardCost : null,
                i.DefaultSalesPrice, i.TaxCodeId, db.TaxCodes.Where(t => t.Id == i.TaxCodeId).Select(t => t.Name).FirstOrDefault(),
                i.StandardLeadHours, i.DifficultyFactor, i.Barcode, i.IsActive, i.CreatedAtUtc, Convert.ToBase64String(i.RowVersion)))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Item));
    }

    public async Task<ItemDto> CreateAsync(ItemUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Manage, ct);
        var type = await ValidateAsync(request, null, ct);
        var showCost = await permissions.HasAsync(Permissions.Items.CostRead, ct);

        var id = await db.InTransactionAsync(async innerCt =>
        {
            var sku = MasterData.NormalizeCode(request.Sku)
                      ?? await MasterData.NextCodeAsync(numbering, MasterDataSequences.Item, DateOnly.FromDateTime(clock.UtcNow),
                          c => db.Items.AnyAsync(x => x.Sku == c, innerCt), innerCt);
            var item = new Item { Sku = sku };
            Apply(item, request, type, showCost);
            db.Items.Add(item);
            await db.SaveChangesAsync(innerCt);
            return item.Id;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<ItemDto> UpdateAsync(Guid id, ItemUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Manage, ct);
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(Item));
        var type = await ValidateAsync(request, id, ct);
        if (MasterData.NormalizeCode(request.Sku) is not { } sku)
        {
            throw ValidationException.For(nameof(request.Sku), "Validation.Required");
        }

        // A service with a BOM can't become a material, and a material used in a BOM can't become a service.
        if (Item.IsServiceType(item.Type) != Item.IsServiceType(type)
            && await db.BomLines.AnyAsync(b => b.ServiceItemId == id || b.MaterialItemId == id, ct))
        {
            throw ValidationException.For(nameof(request.Type), "Validation.TypeLockedByBom");
        }

        db.SetOriginalRowVersion(item, request.RowVersion);
        item.Sku = sku;
        Apply(item, request, type, await permissions.HasAsync(Permissions.Items.CostRead, ct));
        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void Apply(Item i, ItemUpsertRequest r, ItemType type, bool canSetCost)
    {
        i.Name = r.Name.Trim();
        i.NameEn = MasterData.Clean(r.NameEn);
        i.Type = type;
        i.CategoryId = r.CategoryId;
        i.BaseUnitId = r.BaseUnitId;
        i.IsStockTracked = Item.IsStockType(type);
        i.TracksBatches = i.IsStockTracked && r.TracksBatches;
        i.TracksExpiry = i.IsStockTracked && r.TracksExpiry;
        i.ReorderLevel = i.IsStockTracked ? r.ReorderLevel : 0;
        i.ReorderQty = i.IsStockTracked ? r.ReorderQty : 0;
        if (canSetCost && r.StandardCost is { } cost)
        {
            i.StandardCost = cost;
        }

        i.DefaultSalesPrice = r.DefaultSalesPrice;
        i.TaxCodeId = r.TaxCodeId;
        i.StandardLeadHours = Item.IsServiceType(type) ? r.StandardLeadHours : 0;
        i.DifficultyFactor = r.DifficultyFactor;
        i.Barcode = MasterData.Clean(r.Barcode);
    }

    private async Task<ItemType> ValidateAsync(ItemUpsertRequest r, Guid? id, CancellationToken ct)
    {
        var sku = MasterData.NormalizeCode(r.Sku);
        var barcode = MasterData.Clean(r.Barcode);
        var v = new Validator()
            .Pattern(nameof(r.Sku), sku, MasterData.CodePattern, "Validation.Code").MaxLength(nameof(r.Sku), sku, 30)
            .Required(nameof(r.Name), r.Name).MaxLength(nameof(r.Name), r.Name, 200).MaxLength(nameof(r.NameEn), r.NameEn, 200)
            .MaxLength(nameof(r.Barcode), barcode, 50)
            .Range(nameof(r.ReorderLevel), r.ReorderLevel, 0, 10_000_000m).Range(nameof(r.ReorderQty), r.ReorderQty, 0, 10_000_000m)
            .Range(nameof(r.StandardCost), r.StandardCost ?? 0, 0, 100_000_000m)
            .Range(nameof(r.DefaultSalesPrice), r.DefaultSalesPrice, 0, 100_000_000m)
            .Range(nameof(r.StandardLeadHours), r.StandardLeadHours, 0, 2000)
            .Range(nameof(r.DifficultyFactor), r.DifficultyFactor, 0.1m, 10);

        v.When(!MasterData.TryParseEnum<ItemType>(r.Type, out var type), nameof(r.Type), "Validation.Required");
        v.When(!await db.ItemCategories.AnyAsync(c => c.Id == r.CategoryId, ct), nameof(r.CategoryId), "Validation.Required");
        v.When(!await db.Units.AnyAsync(u => u.Id == r.BaseUnitId, ct), nameof(r.BaseUnitId), "Validation.Required");
        if (r.TaxCodeId is { } tax)
        {
            v.When(!await db.TaxCodes.AnyAsync(t => t.Id == tax && t.IsActive, ct), nameof(r.TaxCodeId), "Validation.Required");
        }

        if (sku is not null && await db.Items.AnyAsync(i => i.Sku == sku && i.Id != id, ct))
        {
            v.Add(nameof(r.Sku), "Validation.CodeTaken");
        }

        if (barcode is not null && await db.Items.AnyAsync(i => i.Barcode == barcode && i.Id != id, ct))
        {
            v.Add(nameof(r.Barcode), "Validation.BarcodeTaken");
        }

        v.ThrowIfInvalid();
        return type;
    }

    // ---- Categories (ITM-04) ----

    public async Task<IReadOnlyList<ItemCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        var rows = await db.ItemCategories.AsNoTracking()
            .Select(c => new { c.Id, c.ParentId, c.Name, c.DefaultType, Items = db.Items.Count(i => i.CategoryId == c.Id), c.RowVersion })
            .ToListAsync(ct);
        var names = rows.ToDictionary(r => r.Id, r => r.Name);

        // Parents first, then children, each level by name.
        return rows
            .OrderBy(r => r.ParentId is null ? r.Name : names.GetValueOrDefault(r.ParentId.Value, string.Empty))
            .ThenBy(r => r.ParentId is null ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new ItemCategoryDto(r.Id, r.ParentId, r.ParentId is { } p ? names.GetValueOrDefault(p) : null, r.Name,
                r.DefaultType?.ToString(), r.Items, RowVersions.Encode(r.RowVersion)))
            .ToList();
    }

    public async Task<ItemCategoryDto> SaveCategoryAsync(Guid? id, ItemCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.CategoriesManage, ct);
        var name = request.Name?.Trim();
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150);
        ItemType? defaultType = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultType))
        {
            v.When(!MasterData.TryParseEnum<ItemType>(request.DefaultType, out var t), nameof(request.DefaultType), "Validation.Required");
            defaultType = t;
        }

        if (request.ParentId is { } parentId)
        {
            v.When(!await db.ItemCategories.AnyAsync(c => c.Id == parentId, ct), nameof(request.ParentId), "Validation.Required");
            v.When(id is not null && await IsSelfOrDescendantAsync(parentId, id.Value, ct), nameof(request.ParentId), "Validation.CategoryCycle");
        }

        if (name is { Length: > 0 } && await db.ItemCategories.AnyAsync(c => c.Name == name && c.ParentId == request.ParentId && c.Id != id, ct))
        {
            v.Add(nameof(request.Name), "Validation.NameTaken");
        }

        v.ThrowIfInvalid();

        ItemCategory category;
        if (id is { } existing)
        {
            category = await db.ItemCategories.FirstOrDefaultAsync(c => c.Id == existing, ct) ?? throw new NotFoundException(nameof(ItemCategory));
            db.SetOriginalRowVersion(category, request.RowVersion);
        }
        else
        {
            category = db.ItemCategories.Add(new ItemCategory()).Entity;
        }

        category.Name = name!;
        category.ParentId = request.ParentId;
        category.DefaultType = defaultType;
        await db.SaveChangesAsync(ct);
        return (await ListCategoriesAsync(ct)).First(c => c.Id == category.Id);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.CategoriesManage, ct);
        var category = await db.ItemCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(ItemCategory));
        if (await db.Items.AnyAsync(i => i.CategoryId == id, ct) || await db.ItemCategories.AnyAsync(c => c.ParentId == id, ct))
        {
            throw ValidationException.For("Name", "Validation.InUse");
        }

        db.ItemCategories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> IsSelfOrDescendantAsync(Guid candidate, Guid categoryId, CancellationToken ct)
    {
        var parents = await db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, ct);
        for (Guid? current = candidate; current is { } c; current = parents.GetValueOrDefault(c))
        {
            if (c == categoryId)
            {
                return true;
            }
        }

        return false;
    }

    // ---- BOM (ITM-06) ----

    public async Task<IReadOnlyList<BomLineDto>> ListBomAsync(Guid serviceItemId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        var showCost = await permissions.HasAsync(Permissions.Items.CostRead, ct);
        var lines = await db.BomLines.AsNoTracking().Where(b => b.ServiceItemId == serviceItemId)
            .OrderBy(b => b.MaterialItem.Name)
            .Select(b => new { b.Id, b.MaterialItemId, b.MaterialItem.Sku, b.MaterialItem.Name, b.Quantity, b.UnitId, b.Unit.Code, b.ScrapPercent, b.MaterialItem.StandardCost })
            .ToListAsync(ct);

        return lines.Select(b =>
        {
            var gross = new BomLine { Quantity = b.Quantity, ScrapPercent = b.ScrapPercent }.GrossQuantity;
            return new BomLineDto(b.Id, b.MaterialItemId, b.Sku, b.Name, b.Quantity, b.UnitId, b.Code, b.ScrapPercent, gross,
                showCost ? Math.Round(gross * b.StandardCost, 2) : null);
        }).ToList();
    }

    public async Task<BomLineDto> SaveBomLineAsync(Guid serviceItemId, Guid? id, BomLineRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.BomManage, ct);
        var service = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == serviceItemId, ct) ?? throw new NotFoundException(nameof(Item));
        if (!Item.IsServiceType(service.Type))
        {
            throw ValidationException.For(nameof(request.MaterialItemId), "Validation.BomServiceOnly");
        }

        var material = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.MaterialItemId && i.IsActive, ct);
        var v = new Validator()
            .Range(nameof(request.Quantity), request.Quantity, 0.000001m, 1_000_000m)
            .Range(nameof(request.ScrapPercent), request.ScrapPercent, 0, 100);
        v.When(material is null || !material.IsStockTracked, nameof(request.MaterialItemId), "Validation.BomMaterialOnly");
        v.When(await db.BomLines.AnyAsync(b => b.ServiceItemId == serviceItemId && b.MaterialItemId == request.MaterialItemId && b.Id != id, ct),
            nameof(request.MaterialItemId), "Validation.Duplicate");
        if (material is not null)
        {
            var unitOk = request.UnitId == material.BaseUnitId
                         || await db.UnitConversions.AnyAsync(c => c.ItemId == material.Id && (c.FromUnitId == request.UnitId || c.ToUnitId == request.UnitId), ct);
            v.When(!unitOk, nameof(request.UnitId), "Validation.UnitNotConvertible");
        }

        v.ThrowIfInvalid();

        var line = id is { } existing
            ? await db.BomLines.FirstOrDefaultAsync(b => b.Id == existing && b.ServiceItemId == serviceItemId, ct) ?? throw new NotFoundException(nameof(BomLine))
            : db.BomLines.Add(new BomLine { ServiceItemId = serviceItemId }).Entity;
        line.MaterialItemId = request.MaterialItemId;
        line.Quantity = request.Quantity;
        line.UnitId = request.UnitId;
        line.ScrapPercent = request.ScrapPercent;
        await db.SaveChangesAsync(ct);
        return (await ListBomAsync(serviceItemId, ct)).First(b => b.Id == line.Id);
    }

    public async Task DeleteBomLineAsync(Guid serviceItemId, Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.BomManage, ct);
        var line = await db.BomLines.FirstOrDefaultAsync(b => b.Id == id && b.ServiceItemId == serviceItemId, ct) ?? throw new NotFoundException(nameof(BomLine));
        db.BomLines.Remove(line);
        await db.SaveChangesAsync(ct);
    }

    // ---- Unit conversions (ITM-02) ----

    public async Task<IReadOnlyList<UnitConversionDto>> ListConversionsAsync(Guid itemId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        return await db.UnitConversions.AsNoTracking().Where(c => c.ItemId == itemId).OrderBy(c => c.FromUnit.Code)
            .Select(c => new UnitConversionDto(c.Id, c.FromUnitId, c.FromUnit.Code, c.ToUnitId, c.ToUnit.Code, c.Factor))
            .ToListAsync(ct);
    }

    public async Task AddConversionAsync(Guid itemId, UnitConversionRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Manage, ct);
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new NotFoundException(nameof(Item));
        var v = new Validator().Range(nameof(request.Factor), request.Factor, 0.000001m, 1_000_000m);
        v.When(request.FromUnitId == request.ToUnitId, nameof(request.ToUnitId), "Validation.Duplicate");
        v.When(request.FromUnitId != item.BaseUnitId && request.ToUnitId != item.BaseUnitId, nameof(request.ToUnitId), "Validation.ConversionNeedsBase");
        v.When(await db.Units.CountAsync(u => u.Id == request.FromUnitId || u.Id == request.ToUnitId, ct) != 2, nameof(request.FromUnitId), "Validation.Required");
        v.When(await db.UnitConversions.AnyAsync(c => c.ItemId == itemId
            && ((c.FromUnitId == request.FromUnitId && c.ToUnitId == request.ToUnitId) || (c.FromUnitId == request.ToUnitId && c.ToUnitId == request.FromUnitId)), ct),
            nameof(request.FromUnitId), "Validation.Duplicate");
        v.ThrowIfInvalid();

        db.UnitConversions.Add(new UnitConversion { ItemId = itemId, FromUnitId = request.FromUnitId, ToUnitId = request.ToUnitId, Factor = request.Factor });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteConversionAsync(Guid itemId, Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Manage, ct);
        var c = await db.UnitConversions.FirstOrDefaultAsync(x => x.Id == id && x.ItemId == itemId, ct) ?? throw new NotFoundException(nameof(UnitConversion));
        db.UnitConversions.Remove(c);
        await db.SaveChangesAsync(ct);
    }
}
