using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Catalog;

/// <summary>
/// Price lists (ITM-07/08), bulk updates with preview (ITM-09) and the price quote used by orders and invoices.
/// Exactly one active default list exists per lab; it can't be deactivated, only replaced by another default.
/// </summary>
public sealed class PriceListService(IApplicationDbContext db, IPermissionService permissions, IAuditWriter audit, IClock clock)
{
    public const int MaxBulkPreview = 500;

    public async Task<IReadOnlyList<PriceListDto>> ListAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        return await Project(db.PriceLists.AsNoTracking().OrderByDescending(p => p.IsDefault).ThenByDescending(p => p.IsActive).ThenBy(p => p.Name))
            .ToListAsync(ct);
    }

    public async Task<PriceListDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        return await Project(db.PriceLists.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException(nameof(PriceList));
    }

    private IQueryable<PriceListDto> Project(IQueryable<PriceList> source) =>
        source.Select(p => new PriceListDto(p.Id, p.Name, p.CurrencyCode, p.IsDefault, p.ValidFrom, p.ValidTo, p.IsActive, p.Lines.Count,
            db.Customers.Count(c => c.PriceListId == p.Id), Convert.ToBase64String(p.RowVersion)));

    public async Task<PriceListDto> SaveAsync(Guid? id, PriceListUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Manage, ct);
        var name = request.Name?.Trim();
        var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150)
            .When(request.ValidFrom is { } f && request.ValidTo is { } t && t < f, nameof(request.ValidTo), "Validation.DateRange")
            .When(request.IsDefault && !request.IsActive, nameof(request.IsActive), "Validation.DefaultListActive");
        v.When(currency is null || !await db.Currencies.AnyAsync(c => c.Code == currency, ct), nameof(request.CurrencyCode), "Validation.Required");
        if (name is { Length: > 0 } && await db.PriceLists.AnyAsync(p => p.Name == name && p.Id != id, ct))
        {
            v.Add(nameof(request.Name), "Validation.NameTaken");
        }

        PriceList? list = null;
        if (id is { } existing)
        {
            list = await db.PriceLists.FirstOrDefaultAsync(p => p.Id == existing, ct) ?? throw new NotFoundException(nameof(PriceList));
            v.When(list.IsDefault && !request.IsDefault, nameof(request.IsDefault), "Validation.DefaultListRequired");
            v.When(list.IsDefault && !request.IsActive, nameof(request.IsActive), "Validation.DefaultListActive");
        }

        v.ThrowIfInvalid();

        return await db.InTransactionAsync(async innerCt =>
        {
            if (request.IsDefault && list is not { IsDefault: true })
            {
                // Clear the old default first: the filtered unique index allows one default per lab at any time.
                var old = await db.PriceLists.Where(p => p.IsDefault).ToListAsync(innerCt);
                old.ForEach(p => p.IsDefault = false);
                await db.SaveChangesAsync(innerCt);
            }

            if (list is null)
            {
                list = db.PriceLists.Add(new PriceList()).Entity;
            }
            else
            {
                db.SetOriginalRowVersion(list, request.RowVersion);
            }

            list.Name = name!;
            list.CurrencyCode = currency!;
            list.IsDefault = request.IsDefault;
            list.ValidFrom = request.ValidFrom;
            list.ValidTo = request.ValidTo;
            list.IsActive = request.IsActive;
            await db.SaveChangesAsync(innerCt);
            return await Project(db.PriceLists.AsNoTracking().Where(p => p.Id == list.Id)).FirstAsync(innerCt);
        }, ct);
    }

    // ---- Lines (ITM-08) ----

    public async Task<PagedResult<PriceListLineDto>> ListLinesAsync(Guid priceListId, PageQuery page, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        await EnsureListAsync(priceListId, ct);
        var query = db.PriceListLines.AsNoTracking().Where(l => l.PriceListId == priceListId);
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(l => l.Item.Name.Contains(s) || l.Item.Sku.Contains(s));
        }

        return await query.OrderBy(l => l.Item.Name).ThenBy(l => l.MinQty)
            .Select(l => new PriceListLineDto(l.Id, l.ItemId, l.Item.Sku, l.Item.Name, l.Item.Type.ToString(), l.Price, l.MinQty, l.Item.DefaultSalesPrice))
            .ToPagedAsync(page, ct);
    }

    /// <summary>An item's prices across all lists (ITM-03 prices tab).</summary>
    public async Task<IReadOnlyList<ItemPriceDto>> ListItemPricesAsync(Guid itemId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        return await db.PriceListLines.AsNoTracking().Where(l => l.ItemId == itemId)
            .Join(db.PriceLists, l => l.PriceListId, p => p.Id, (l, p) => new { l, p })
            .OrderByDescending(x => x.p.IsDefault).ThenBy(x => x.p.Name).ThenBy(x => x.l.MinQty)
            .Select(x => new ItemPriceDto(x.p.Id, x.p.Name, x.p.IsDefault, x.p.IsActive, x.l.MinQty, x.l.Price))
            .ToListAsync(ct);
    }

    /// <summary>Adds a line, or updates the price when the item already has a line for that minimum quantity.</summary>
    public async Task SetLineAsync(Guid priceListId, PriceListLineRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Manage, ct);
        await EnsureListAsync(priceListId, ct);
        var v = new Validator().Range(nameof(request.Price), request.Price, 0, 100_000_000m).Range(nameof(request.MinQty), request.MinQty, 1, 1_000_000m);
        v.When(!await db.Items.AnyAsync(i => i.Id == request.ItemId && i.IsActive, ct), nameof(request.ItemId), "Validation.Required");
        v.ThrowIfInvalid();

        var line = await db.PriceListLines.FirstOrDefaultAsync(l => l.PriceListId == priceListId && l.ItemId == request.ItemId && l.MinQty == request.MinQty, ct);
        if (line is null)
        {
            db.PriceListLines.Add(new PriceListLine { PriceListId = priceListId, ItemId = request.ItemId, Price = request.Price, MinQty = request.MinQty });
        }
        else
        {
            line.Price = request.Price;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteLineAsync(Guid priceListId, Guid lineId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Manage, ct);
        var line = await db.PriceListLines.FirstOrDefaultAsync(l => l.Id == lineId && l.PriceListId == priceListId, ct)
                   ?? throw new NotFoundException(nameof(PriceListLine));
        db.PriceListLines.Remove(line);
        await db.SaveChangesAsync(ct);
    }

    // ---- Bulk update (ITM-09) ----

    public async Task<BulkPricePreviewDto> PreviewBulkAsync(BulkPriceUpdateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Bulk, ct);
        var lines = await BulkLinesAsync(request, tracking: false, ct);
        var changes = lines.Select(l => new BulkPriceChangeDto(l.ItemId, l.Item.Sku, l.Item.Name, l.MinQty, l.Price,
            PriceResolver.AdjustByPercent(l.Price, request.Percent, request.Decimals))).ToList();
        return new BulkPricePreviewDto(changes.Count, changes.Sum(c => c.OldPrice), changes.Sum(c => c.NewPrice), changes.Take(MaxBulkPreview).ToList());
    }

    public async Task<int> ApplyBulkAsync(BulkPriceUpdateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Bulk, ct);
        var lines = await BulkLinesAsync(request, tracking: true, ct);
        foreach (var line in lines)
        {
            line.Price = PriceResolver.AdjustByPercent(line.Price, request.Percent, request.Decimals);
        }

        audit.Add(AuditActions.Update, nameof(PriceList), request.PriceListId, "BulkPriceUpdate",
            new { request.Percent, request.CategoryId, request.ItemType, request.Decimals, lines = lines.Count });
        await db.SaveChangesAsync(ct);
        return lines.Count;
    }

    private async Task<List<PriceListLine>> BulkLinesAsync(BulkPriceUpdateRequest r, bool tracking, CancellationToken ct)
    {
        var v = new Validator().Range(nameof(r.Percent), r.Percent, -90, 500).Range(nameof(r.Decimals), r.Decimals, 0, 3)
            .When(r.Percent == 0, nameof(r.Percent), "Validation.Range");
        ItemType? type = null;
        if (!string.IsNullOrWhiteSpace(r.ItemType))
        {
            v.When(!MasterData.TryParseEnum<ItemType>(r.ItemType, out var t), nameof(r.ItemType), "Validation.Required");
            type = t;
        }

        v.When(!await db.PriceLists.AnyAsync(p => p.Id == r.PriceListId, ct), nameof(r.PriceListId), "Validation.Required");
        v.ThrowIfInvalid();

        var query = db.PriceListLines.Include(l => l.Item).Where(l => l.PriceListId == r.PriceListId);
        if (r.CategoryId is { } categoryId)
        {
            query = query.Where(l => l.Item.CategoryId == categoryId);
        }

        if (type is { } itemType)
        {
            query = query.Where(l => l.Item.Type == itemType);
        }

        return await (tracking ? query : query.AsNoTracking()).OrderBy(l => l.Item.Name).ThenBy(l => l.MinQty).ToListAsync(ct);
    }

    // ---- Quote ----

    /// <summary>The price a customer pays for an item today (customer price ← customer/group list ← default list ← item price).</summary>
    public async Task<PriceQuoteDto> QuoteAsync(Guid customerId, Guid itemId, decimal quantity, DateOnly? date = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        var on = date ?? DateOnly.FromDateTime(clock.UtcNow);
        var customer = await db.Customers.AsNoTracking().Include(c => c.Group).FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException(nameof(Customer));
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new NotFoundException(nameof(Item));

        var (resolved, customerList, defaultList) = await ResolveCoreAsync(customer, item, quantity, on, ct);
        var listName = resolved.PriceListId == customerList?.Id ? customerList?.Name : resolved.PriceListId == defaultList?.Id ? defaultList?.Name : null;
        return new PriceQuoteDto(itemId, customerId, quantity, resolved.Price, resolved.Source.ToString(), resolved.PriceListId, listName);
    }

    /// <summary>The price an order line gets (no permission check: callers already hold the document's permission).</summary>
    public async Task<ResolvedPrice> ResolveAsync(Guid customerId, Item item, decimal quantity, DateOnly on, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking().Include(c => c.Group).FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException(nameof(Customer));
        return (await ResolveCoreAsync(customer, item, quantity, on, ct)).Resolved;
    }

    private async Task<(ResolvedPrice Resolved, PriceList? CustomerList, PriceList? DefaultList)> ResolveCoreAsync(
        Customer customer, Item item, decimal quantity, DateOnly on, CancellationToken ct)
    {
        var customerId = customer.Id;
        var itemId = item.Id;
        var specials = await db.CustomerItemPrices.AsNoTracking().Where(p => p.CustomerId == customerId && p.ItemId == itemId).ToListAsync(ct);
        var customerListId = customer.PriceListId ?? customer.Group?.DefaultPriceListId;
        var customerList = customerListId is { } cl ? await LoadListAsync(cl, itemId, ct) : null;
        var defaultList = await db.PriceLists.AsNoTracking().Where(p => p.IsDefault).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct) is { } dl
            ? await LoadListAsync(dl, itemId, ct)
            : null;

        return (PriceResolver.Resolve(item, quantity, on, specials, customerList, defaultList), customerList, defaultList);
    }

    private Task<PriceList?> LoadListAsync(Guid id, Guid itemId, CancellationToken ct) =>
        db.PriceLists.AsNoTracking().Include(p => p.Lines.Where(l => l.ItemId == itemId)).FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task EnsureListAsync(Guid id, CancellationToken ct)
    {
        if (!await db.PriceLists.AnyAsync(p => p.Id == id, ct))
        {
            throw new NotFoundException(nameof(PriceList));
        }
    }
}
