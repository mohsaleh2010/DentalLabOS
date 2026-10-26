using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Parties;

public sealed record CustomerFilter(bool? Active = null, Guid? GroupId = null, string? Type = null);

/// <summary>
/// Customers (CUS-01..05): clinics, centers and independent dentists, their dentists, groups and special prices.
/// Customers are deactivated, never deleted, because orders and invoices reference them.
/// </summary>
public sealed class CustomerService(IApplicationDbContext db, IPermissionService permissions, INumberingService numbering, IClock clock)
{
    public async Task<PagedResult<CustomerListItemDto>> ListAsync(PageQuery page, CustomerFilter? filter = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Read, ct);

        var query = db.Customers.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(c => c.Name.Contains(s) || c.Code.Contains(s) || (c.Phone != null && c.Phone.Contains(s))
                                     || (c.Mobile != null && c.Mobile.Contains(s)) || c.Dentists.Any(d => d.FullName.Contains(s)));
        }

        if (filter?.Active is { } active)
        {
            query = query.Where(c => c.IsActive == active);
        }

        if (filter?.GroupId is { } groupId)
        {
            query = query.Where(c => c.GroupId == groupId);
        }

        if (MasterData.TryParseEnum<PartyType>(filter?.Type, out var type))
        {
            query = query.Where(c => c.Type == type);
        }

        query = page.Sort switch
        {
            "code" => query.OrderBy(c => c.Code),
            "newest" => query.OrderByDescending(c => c.CreatedAtUtc),
            _ => query.OrderBy(c => c.Name),
        };

        return await query
            .Select(c => new CustomerListItemDto(c.Id, c.Code, c.Name, c.Type.ToString(), c.Group != null ? c.Group.Name : null, c.Phone, c.Mobile,
                c.City, c.Dentists.Count(d => d.IsActive), c.CreditLimit, c.IsActive))
            .ToPagedAsync(page, ct);
    }

    /// <summary>Active customers for pickers (top matches only).</summary>
    public async Task<IReadOnlyList<OptionDto>> OptionsAsync(string? search, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Read, ct);
        var query = db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (MasterData.Clean(search) is { } s)
        {
            query = query.Where(c => c.Name.Contains(s) || c.Code.Contains(s));
        }

        return await query.OrderBy(c => c.Name).Take(50).Select(c => new OptionDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Read, ct);
        return await db.Customers.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.Type.ToString(), c.GroupId, c.Group != null ? c.Group.Name : null, c.Phone,
                c.Mobile, c.Email, c.Address, c.City, c.TaxNumber, c.PaymentTermsDays, c.CreditLimit, c.BlockWhenOverCredit, c.PriceListId,
                db.PriceLists.Where(p => p.Id == c.PriceListId).Select(p => p.Name).FirstOrDefault(), c.OpeningBalance, c.IsActive,
                c.InternalNotes, c.CreatedAtUtc, Convert.ToBase64String(c.RowVersion)))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Customer));
    }

    public async Task<CustomerDto> CreateAsync(CustomerUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Manage, ct);
        var type = await ValidateAsync(request, null, ct);
        await DemandCreditChangeAsync(request.CreditLimit != 0 || request.BlockWhenOverCredit, ct);

        var id = await db.InTransactionAsync(async innerCt =>
        {
            var code = MasterData.NormalizeCode(request.Code)
                       ?? await MasterData.NextCodeAsync(numbering, MasterDataSequences.Customer, Today(),
                           c => db.Customers.AnyAsync(x => x.Code == c, innerCt), innerCt);
            var customer = new Customer { Code = code };
            Apply(customer, request, type);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(innerCt);
            return customer.Id;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, CustomerUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Manage, ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(Customer));
        var type = await ValidateAsync(request, id, ct);
        if (MasterData.NormalizeCode(request.Code) is null)
        {
            throw ValidationException.For(nameof(request.Code), "Validation.Required");
        }

        await DemandCreditChangeAsync(request.CreditLimit != customer.CreditLimit || request.BlockWhenOverCredit != customer.BlockWhenOverCredit, ct);

        db.SetOriginalRowVersion(customer, request.RowVersion);
        customer.Code = MasterData.NormalizeCode(request.Code)!;
        Apply(customer, request, type);
        customer.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void Apply(Customer c, CustomerUpsertRequest r, PartyType type)
    {
        c.Name = r.Name.Trim();
        c.Type = type;
        c.GroupId = r.GroupId;
        c.Phone = MasterData.Clean(r.Phone);
        c.Mobile = MasterData.Clean(r.Mobile);
        c.Email = MasterData.Clean(r.Email);
        c.Address = MasterData.Clean(r.Address);
        c.City = MasterData.Clean(r.City);
        c.TaxNumber = MasterData.Clean(r.TaxNumber);
        c.PaymentTermsDays = r.PaymentTermsDays;
        c.CreditLimit = r.CreditLimit;
        c.BlockWhenOverCredit = r.BlockWhenOverCredit;
        c.PriceListId = r.PriceListId;
        c.OpeningBalance = r.OpeningBalance;
        c.InternalNotes = MasterData.Clean(r.InternalNotes);
    }

    /// <summary>Credit terms are a finance decision (CUS-07): changing them needs <c>customers.credit.manage</c>.</summary>
    private async Task DemandCreditChangeAsync(bool changing, CancellationToken ct)
    {
        if (changing && !await permissions.HasAsync(Permissions.Customers.CreditManage, ct))
        {
            throw ValidationException.For(nameof(CustomerUpsertRequest.CreditLimit), "Validation.CreditPermission");
        }
    }

    private async Task<PartyType> ValidateAsync(CustomerUpsertRequest r, Guid? id, CancellationToken ct)
    {
        var code = MasterData.NormalizeCode(r.Code);
        var v = new Validator()
            .Pattern(nameof(r.Code), code, MasterData.CodePattern, "Validation.Code").MaxLength(nameof(r.Code), code, 20)
            .Required(nameof(r.Name), r.Name).MaxLength(nameof(r.Name), r.Name, 200)
            .MaxLength(nameof(r.Phone), r.Phone, 50).MaxLength(nameof(r.Mobile), r.Mobile, 50)
            .Email(nameof(r.Email), r.Email).MaxLength(nameof(r.Email), r.Email, 256)
            .MaxLength(nameof(r.Address), r.Address, 500).MaxLength(nameof(r.City), r.City, 100)
            .MaxLength(nameof(r.TaxNumber), r.TaxNumber, 50).MaxLength(nameof(r.InternalNotes), r.InternalNotes, 2000)
            .Range(nameof(r.PaymentTermsDays), r.PaymentTermsDays, 0, 365)
            .Range(nameof(r.CreditLimit), r.CreditLimit, 0, 1_000_000_000m)
            .Range(nameof(r.OpeningBalance), r.OpeningBalance, -1_000_000_000m, 1_000_000_000m);

        v.When(!MasterData.TryParseEnum<PartyType>(r.Type, out var type), nameof(r.Type), "Validation.Required");

        if (code is not null && await db.Customers.AnyAsync(c => c.Code == code && c.Id != id, ct))
        {
            v.Add(nameof(r.Code), "Validation.CodeTaken");
        }

        if (r.GroupId is { } g && !await db.CustomerGroups.AnyAsync(x => x.Id == g, ct))
        {
            v.Add(nameof(r.GroupId), "Validation.Required");
        }

        if (r.PriceListId is { } p && !await db.PriceLists.AnyAsync(x => x.Id == p && x.IsActive, ct))
        {
            v.Add(nameof(r.PriceListId), "Validation.Required");
        }

        v.ThrowIfInvalid();
        return type;
    }

    // ---- Groups (CUS-05) ----

    public async Task<IReadOnlyList<CustomerGroupDto>> ListGroupsAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Read, ct);
        return await db.CustomerGroups.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new CustomerGroupDto(g.Id, g.Name, g.DefaultPriceListId,
                db.PriceLists.Where(p => p.Id == g.DefaultPriceListId).Select(p => p.Name).FirstOrDefault(),
                db.Customers.Count(c => c.GroupId == g.Id), Convert.ToBase64String(g.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task<CustomerGroupDto> SaveGroupAsync(Guid? id, CustomerGroupUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.GroupsManage, ct);
        var name = request.Name?.Trim();
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150);
        if (name is { Length: > 0 } && await db.CustomerGroups.AnyAsync(g => g.Name == name && g.Id != id, ct))
        {
            v.Add(nameof(request.Name), "Validation.NameTaken");
        }

        if (request.DefaultPriceListId is { } p && !await db.PriceLists.AnyAsync(x => x.Id == p && x.IsActive, ct))
        {
            v.Add(nameof(request.DefaultPriceListId), "Validation.Required");
        }

        v.ThrowIfInvalid();

        CustomerGroup group;
        if (id is { } existing)
        {
            group = await db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == existing, ct) ?? throw new NotFoundException(nameof(CustomerGroup));
            db.SetOriginalRowVersion(group, request.RowVersion);
        }
        else
        {
            group = new CustomerGroup();
            db.CustomerGroups.Add(group);
        }

        group.Name = name!;
        group.DefaultPriceListId = request.DefaultPriceListId;
        await db.SaveChangesAsync(ct);
        return (await ListGroupsAsync(ct)).First(g => g.Id == group.Id);
    }

    public async Task DeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.GroupsManage, ct);
        var group = await db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException(nameof(CustomerGroup));
        if (await db.Customers.AnyAsync(c => c.GroupId == id, ct))
        {
            throw ValidationException.For("Name", "Validation.InUse");
        }

        db.CustomerGroups.Remove(group);
        await db.SaveChangesAsync(ct);
    }

    // ---- Dentists (CUS-04) ----

    public async Task<IReadOnlyList<DentistDto>> ListDentistsAsync(Guid customerId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.Read, ct);
        await EnsureCustomerAsync(customerId, ct);
        return await db.Dentists.AsNoTracking().Where(d => d.CustomerId == customerId)
            .OrderByDescending(d => d.IsActive).ThenBy(d => d.FullName)
            .Select(d => new DentistDto(d.Id, d.CustomerId, d.FullName, d.Specialty, d.Phone, d.Email, d.IsActive, Convert.ToBase64String(d.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task<DentistDto> SaveDentistAsync(Guid customerId, Guid? id, DentistUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Customers.DentistsManage, ct);
        await EnsureCustomerAsync(customerId, ct);
        new Validator()
            .Required(nameof(request.FullName), request.FullName).MaxLength(nameof(request.FullName), request.FullName, 150)
            .MaxLength(nameof(request.Specialty), request.Specialty, 100).MaxLength(nameof(request.Phone), request.Phone, 50)
            .Email(nameof(request.Email), request.Email).MaxLength(nameof(request.Email), request.Email, 256)
            .ThrowIfInvalid();

        Dentist dentist;
        if (id is { } existing)
        {
            dentist = await db.Dentists.FirstOrDefaultAsync(d => d.Id == existing && d.CustomerId == customerId, ct)
                      ?? throw new NotFoundException(nameof(Dentist));
            db.SetOriginalRowVersion(dentist, request.RowVersion);
            dentist.IsActive = request.IsActive;
        }
        else
        {
            dentist = new Dentist { CustomerId = customerId };
            db.Dentists.Add(dentist);
        }

        dentist.FullName = request.FullName.Trim();
        dentist.Specialty = MasterData.Clean(request.Specialty);
        dentist.Phone = MasterData.Clean(request.Phone);
        dentist.Email = MasterData.Clean(request.Email);
        await db.SaveChangesAsync(ct);
        return new DentistDto(dentist.Id, customerId, dentist.FullName, dentist.Specialty, dentist.Phone, dentist.Email, dentist.IsActive,
            RowVersions.Encode(dentist.RowVersion));
    }

    // ---- Special prices (CUS-03 prices tab) ----

    public async Task<IReadOnlyList<CustomerPriceDto>> ListPricesAsync(Guid customerId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Read, ct);
        await EnsureCustomerAsync(customerId, ct);
        return await db.CustomerItemPrices.AsNoTracking().Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.Item.Name).ThenBy(p => p.ValidFrom)
            .Select(p => new CustomerPriceDto(p.Id, p.ItemId, p.Item.Sku, p.Item.Name, p.Price, p.Item.DefaultSalesPrice, p.ValidFrom, p.ValidTo))
            .ToListAsync(ct);
    }

    public async Task<CustomerPriceDto> AddPriceAsync(Guid customerId, CustomerPriceUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Manage, ct);
        await EnsureCustomerAsync(customerId, ct);

        var v = new Validator().Range(nameof(request.Price), request.Price, 0, 100_000_000m)
            .When(request.ValidFrom is { } f && request.ValidTo is { } t && t < f, nameof(request.ValidTo), "Validation.DateRange");
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ItemId && i.IsActive, ct);
        v.When(item is null, nameof(request.ItemId), "Validation.Required");

        // Overlapping periods would make the price ambiguous.
        var overlaps = await db.CustomerItemPrices.AnyAsync(p => p.CustomerId == customerId && p.ItemId == request.ItemId
            && (p.ValidFrom == null || request.ValidTo == null || p.ValidFrom <= request.ValidTo)
            && (p.ValidTo == null || request.ValidFrom == null || p.ValidTo >= request.ValidFrom), ct);
        v.When(overlaps, nameof(request.ValidFrom), "Validation.PriceOverlap");
        v.ThrowIfInvalid();

        var price = new CustomerItemPrice
        {
            CustomerId = customerId, ItemId = request.ItemId, Price = request.Price, ValidFrom = request.ValidFrom, ValidTo = request.ValidTo,
        };
        db.CustomerItemPrices.Add(price);
        await db.SaveChangesAsync(ct);
        return new CustomerPriceDto(price.Id, item!.Id, item.Sku, item.Name, price.Price, item.DefaultSalesPrice, price.ValidFrom, price.ValidTo);
    }

    public async Task DeletePriceAsync(Guid customerId, Guid priceId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.PriceLists.Manage, ct);
        var price = await db.CustomerItemPrices.FirstOrDefaultAsync(p => p.Id == priceId && p.CustomerId == customerId, ct)
                    ?? throw new NotFoundException(nameof(CustomerItemPrice));
        db.CustomerItemPrices.Remove(price);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCustomerAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException(nameof(Customer));
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow);
}
