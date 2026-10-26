using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Parties;

/// <summary>Suppliers (SUP-01/02/03/05) and the items they supply. Purchase prices are hidden without the cost permission.</summary>
public sealed class SupplierService(IApplicationDbContext db, IPermissionService permissions, INumberingService numbering, IClock clock)
{
    public async Task<PagedResult<SupplierListItemDto>> ListAsync(PageQuery page, bool? active = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Read, ct);

        var query = db.Suppliers.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(x => x.Name.Contains(s) || x.Code.Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }

        if (active is { } a)
        {
            query = query.Where(x => x.IsActive == a);
        }

        query = page.Sort == "code" ? query.OrderBy(x => x.Code) : query.OrderBy(x => x.Name);
        return await query
            .Select(x => new SupplierListItemDto(x.Id, x.Code, x.Name, x.Phone, x.Email, x.Rating, x.IsOutsourcePartner, x.Items.Count, x.IsActive))
            .ToPagedAsync(page, ct);
    }

    public async Task<IReadOnlyList<OptionDto>> OptionsAsync(string? search, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Read, ct);
        var query = db.Suppliers.AsNoTracking().Where(x => x.IsActive);
        if (MasterData.Clean(search) is { } s)
        {
            query = query.Where(x => x.Name.Contains(s) || x.Code.Contains(s));
        }

        return await query.OrderBy(x => x.Name).Take(50).Select(x => new OptionDto(x.Id, x.Code, x.Name)).ToListAsync(ct);
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Read, ct);
        var x = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(Supplier));
        return Map(x);
    }

    public async Task<SupplierDto> CreateAsync(SupplierUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Manage, ct);
        await ValidateAsync(request, null, ct);

        return await db.InTransactionAsync(async innerCt =>
        {
            var code = MasterData.NormalizeCode(request.Code)
                       ?? await MasterData.NextCodeAsync(numbering, MasterDataSequences.Supplier, DateOnly.FromDateTime(clock.UtcNow),
                           c => db.Suppliers.AnyAsync(x => x.Code == c, innerCt), innerCt);
            var supplier = new Supplier { Code = code };
            Apply(supplier, request);
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(innerCt);
            return Map(supplier);
        }, ct);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SupplierUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Manage, ct);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(Supplier));
        await ValidateAsync(request, id, ct);
        if (MasterData.NormalizeCode(request.Code) is not { } code)
        {
            throw ValidationException.For(nameof(request.Code), "Validation.Required");
        }

        db.SetOriginalRowVersion(supplier, request.RowVersion);
        supplier.Code = code;
        Apply(supplier, request);
        supplier.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Map(supplier);
    }

    private static void Apply(Supplier s, SupplierUpsertRequest r)
    {
        s.Name = r.Name.Trim();
        s.Phone = MasterData.Clean(r.Phone);
        s.Email = MasterData.Clean(r.Email);
        s.Address = MasterData.Clean(r.Address);
        s.TaxNumber = MasterData.Clean(r.TaxNumber);
        s.PaymentTermsDays = r.PaymentTermsDays;
        s.OpeningBalance = r.OpeningBalance;
        s.Rating = r.Rating;
        s.IsOutsourcePartner = r.IsOutsourcePartner;
    }

    private async Task ValidateAsync(SupplierUpsertRequest r, Guid? id, CancellationToken ct)
    {
        var code = MasterData.NormalizeCode(r.Code);
        var v = new Validator()
            .Pattern(nameof(r.Code), code, MasterData.CodePattern, "Validation.Code").MaxLength(nameof(r.Code), code, 20)
            .Required(nameof(r.Name), r.Name).MaxLength(nameof(r.Name), r.Name, 200)
            .MaxLength(nameof(r.Phone), r.Phone, 50).Email(nameof(r.Email), r.Email).MaxLength(nameof(r.Email), r.Email, 256)
            .MaxLength(nameof(r.Address), r.Address, 500).MaxLength(nameof(r.TaxNumber), r.TaxNumber, 50)
            .Range(nameof(r.PaymentTermsDays), r.PaymentTermsDays, 0, 365)
            .Range(nameof(r.OpeningBalance), r.OpeningBalance, -1_000_000_000m, 1_000_000_000m)
            .Range(nameof(r.Rating), r.Rating ?? 0, 0, 5);

        if (code is not null && await db.Suppliers.AnyAsync(s => s.Code == code && s.Id != id, ct))
        {
            v.Add(nameof(r.Code), "Validation.CodeTaken");
        }

        v.ThrowIfInvalid();
    }

    private static SupplierDto Map(Supplier x) =>
        new(x.Id, x.Code, x.Name, x.Phone, x.Email, x.Address, x.TaxNumber, x.PaymentTermsDays, x.OpeningBalance, x.Rating,
            x.IsOutsourcePartner, x.IsActive, x.CreatedAtUtc, RowVersions.Encode(x.RowVersion));

    // ---- Supplier items (SUP-05) ----

    public async Task<IReadOnlyList<SupplierItemDto>> ListItemsAsync(Guid supplierId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.Read, ct);
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
        {
            throw new NotFoundException(nameof(Supplier));
        }

        return await QueryItems(db.SupplierItems.Where(x => x.SupplierId == supplierId), await CanSeeCostAsync(ct)).ToListAsync(ct);
    }

    /// <summary>Who supplies an item (ITM-03 suppliers tab).</summary>
    public async Task<IReadOnlyList<SupplierItemDto>> ListForItemAsync(Guid itemId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Items.Read, ct);
        return await QueryItems(db.SupplierItems.Where(x => x.ItemId == itemId), await CanSeeCostAsync(ct)).ToListAsync(ct);
    }

    public async Task<SupplierItemDto> SaveItemAsync(Guid supplierId, Guid? id, SupplierItemUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.ItemsManage, ct);
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
        {
            throw new NotFoundException(nameof(Supplier));
        }

        var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
        var v = new Validator()
            .MaxLength(nameof(request.SupplierSku), request.SupplierSku, 50)
            .Range(nameof(request.LastPrice), request.LastPrice, 0, 100_000_000m)
            .Range(nameof(request.LeadTimeDays), request.LeadTimeDays, 0, 365)
            .Range(nameof(request.MinOrderQty), request.MinOrderQty, 0, 1_000_000m);
        v.When(!await db.Items.AnyAsync(i => i.Id == request.ItemId && i.IsActive, ct), nameof(request.ItemId), "Validation.Required");
        v.When(currency is null || !await db.Currencies.AnyAsync(c => c.Code == currency, ct), nameof(request.CurrencyCode), "Validation.Required");
        v.When(await db.SupplierItems.AnyAsync(x => x.SupplierId == supplierId && x.ItemId == request.ItemId && x.Id != id, ct),
            nameof(request.ItemId), "Validation.Duplicate");
        v.ThrowIfInvalid();

        var entity = id is { } existing
            ? await db.SupplierItems.FirstOrDefaultAsync(x => x.Id == existing && x.SupplierId == supplierId, ct) ?? throw new NotFoundException(nameof(SupplierItem))
            : db.SupplierItems.Add(new SupplierItem { SupplierId = supplierId }).Entity;

        entity.ItemId = request.ItemId;
        entity.SupplierSku = MasterData.Clean(request.SupplierSku);
        // Without the cost permission the price is never shown, so an edit must not overwrite it.
        if (id is null || await CanSeeCostAsync(ct))
        {
            entity.LastPrice = request.LastPrice;
        }

        entity.CurrencyCode = currency!;
        entity.LeadTimeDays = request.LeadTimeDays;
        entity.MinOrderQty = request.MinOrderQty;
        entity.IsPreferred = request.IsPreferred;

        // One preferred supplier per item.
        if (request.IsPreferred)
        {
            var others = await db.SupplierItems.Where(x => x.ItemId == request.ItemId && x.Id != entity.Id && x.IsPreferred).ToListAsync(ct);
            others.ForEach(o => o.IsPreferred = false);
        }

        await db.SaveChangesAsync(ct);
        return await QueryItems(db.SupplierItems.Where(x => x.Id == entity.Id), true).FirstAsync(ct);
    }

    public async Task DeleteItemAsync(Guid supplierId, Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Suppliers.ItemsManage, ct);
        var entity = await db.SupplierItems.FirstOrDefaultAsync(x => x.Id == id && x.SupplierId == supplierId, ct)
                     ?? throw new NotFoundException(nameof(SupplierItem));
        db.SupplierItems.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private Task<bool> CanSeeCostAsync(CancellationToken ct) => permissions.HasAsync(Permissions.Items.CostRead, ct);

    private IQueryable<SupplierItemDto> QueryItems(IQueryable<SupplierItem> source, bool showCost) =>
        source.AsNoTracking().OrderByDescending(x => x.IsPreferred).ThenBy(x => x.Item.Name)
            .Select(x => new SupplierItemDto(x.Id, x.SupplierId, db.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.Name).First(),
                x.ItemId, x.Item.Sku, x.Item.Name, x.SupplierSku, showCost ? x.LastPrice : null, x.CurrencyCode, x.LeadTimeDays, x.MinOrderQty,
                x.IsPreferred));
}
