using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Catalog;

/// <summary>Units (ADM-14), tax codes (ADM-12), currencies and exchange rates (ADM-13).</summary>
public sealed class ReferenceDataService(IApplicationDbContext db, IPermissionService permissions, ITenantContext tenant)
{
    private const string UnitCodePattern = "^[A-Za-z0-9_.-]{1,10}$";
    private const string CurrencyCodePattern = "^[A-Za-z]{3}$";

    // ---- Units ----

    /// <summary>Units are needed by every item form, so any user who can read items may list them.</summary>
    public async Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Admin.UnitsManage, ct))
        {
            await permissions.DemandAsync(Permissions.Items.Read, ct);
        }

        return await db.Units.AsNoTracking().OrderBy(u => u.Code)
            .Select(u => new UnitDto(u.Id, u.Code, u.Name, u.DecimalPlaces, db.Items.Count(i => i.BaseUnitId == u.Id), Convert.ToBase64String(u.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task SaveUnitAsync(Guid? id, UnitUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UnitsManage, ct);
        var code = request.Code?.Trim().ToUpperInvariant();
        var v = new Validator()
            .Required(nameof(request.Code), code).Pattern(nameof(request.Code), code, UnitCodePattern, "Validation.Code")
            .Required(nameof(request.Name), request.Name).MaxLength(nameof(request.Name), request.Name, 50)
            .Range(nameof(request.DecimalPlaces), request.DecimalPlaces, 0, 6);
        v.When(code is { Length: > 0 } && await db.Units.AnyAsync(u => u.Code == code && u.Id != id, ct), nameof(request.Code), "Validation.CodeTaken");
        v.ThrowIfInvalid();

        var unit = id is { } existing
            ? await db.Units.FirstOrDefaultAsync(u => u.Id == existing, ct) ?? throw new NotFoundException(nameof(UnitOfMeasure))
            : db.Units.Add(new UnitOfMeasure()).Entity;
        if (id is not null)
        {
            db.SetOriginalRowVersion(unit, request.RowVersion);
        }

        unit.Code = code!;
        unit.Name = request.Name.Trim();
        unit.DecimalPlaces = request.DecimalPlaces;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteUnitAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UnitsManage, ct);
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException(nameof(UnitOfMeasure));
        var used = await db.Items.AnyAsync(i => i.BaseUnitId == id, ct)
                   || await db.UnitConversions.AnyAsync(c => c.FromUnitId == id || c.ToUnitId == id, ct)
                   || await db.BomLines.AnyAsync(b => b.UnitId == id, ct);
        if (used)
        {
            throw ValidationException.For("Code", "Validation.InUse");
        }

        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
    }

    // ---- Tax codes ----

    public async Task<IReadOnlyList<TaxCodeDto>> ListTaxCodesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(Permissions.Admin.TaxesManage, ct))
        {
            await permissions.DemandAsync(Permissions.Items.Read, ct);
            activeOnly = true;
        }

        return await db.TaxCodes.AsNoTracking().Where(t => !activeOnly || t.IsActive).OrderBy(t => t.Code)
            .Select(t => new TaxCodeDto(t.Id, t.Code, t.Name, t.Rate, t.IsWithholding, t.IsActive, Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task SaveTaxCodeAsync(Guid? id, TaxCodeUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.TaxesManage, ct);
        var code = MasterData.NormalizeCode(request.Code);
        var v = new Validator()
            .Required(nameof(request.Code), code).Pattern(nameof(request.Code), code, MasterData.CodePattern, "Validation.Code")
            .MaxLength(nameof(request.Code), code, 20)
            .Required(nameof(request.Name), request.Name).MaxLength(nameof(request.Name), request.Name, 150)
            .Range(nameof(request.Rate), request.Rate, 0, 100);
        v.When(code is not null && await db.TaxCodes.AnyAsync(t => t.Code == code && t.Id != id, ct), nameof(request.Code), "Validation.CodeTaken");
        v.ThrowIfInvalid();

        var tax = id is { } existing
            ? await db.TaxCodes.FirstOrDefaultAsync(t => t.Id == existing, ct) ?? throw new NotFoundException(nameof(TaxCode))
            : db.TaxCodes.Add(new TaxCode()).Entity;
        if (id is not null)
        {
            db.SetOriginalRowVersion(tax, request.RowVersion);
        }

        tax.Code = code!;
        tax.Name = request.Name.Trim();
        tax.Rate = request.Rate;
        tax.IsWithholding = request.IsWithholding;
        tax.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
    }

    // ---- Currencies & rates ----

    public async Task<IReadOnlyList<CurrencyDto>> ListCurrenciesAsync(CancellationToken ct = default)
    {
        // Currency codes are needed by price lists and supplier prices.
        if (!await permissions.HasAsync(Permissions.Admin.CurrenciesManage, ct) && !await permissions.HasAsync(Permissions.PriceLists.Read, ct))
        {
            await permissions.DemandAsync(Permissions.Suppliers.Read, ct);
        }

        var baseCode = await BaseCurrencyAsync(ct);
        return await db.Currencies.AsNoTracking().OrderByDescending(c => c.Code == baseCode).ThenBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.DecimalPlaces, c.Code == baseCode, Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task SaveCurrencyAsync(Guid? id, CurrencyUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.CurrenciesManage, ct);
        var code = request.Code?.Trim().ToUpperInvariant();
        var v = new Validator()
            .Required(nameof(request.Code), code).Pattern(nameof(request.Code), code, CurrencyCodePattern, "Validation.CurrencyCode")
            .Required(nameof(request.Name), request.Name).MaxLength(nameof(request.Name), request.Name, 100)
            .Range(nameof(request.DecimalPlaces), request.DecimalPlaces, 0, 4);
        v.When(code is { Length: > 0 } && await db.Currencies.AnyAsync(c => c.Code == code && c.Id != id, ct), nameof(request.Code), "Validation.CodeTaken");

        Currency? currency = null;
        if (id is { } existing)
        {
            currency = await db.Currencies.FirstOrDefaultAsync(c => c.Id == existing, ct) ?? throw new NotFoundException(nameof(Currency));
            // Documents and lists keep the code, so it's fixed once created.
            v.When(currency.Code != code, nameof(request.Code), "Validation.CodeLocked");
        }

        v.ThrowIfInvalid();
        if (currency is null)
        {
            currency = db.Currencies.Add(new Currency { Code = code! }).Entity;
        }
        else
        {
            db.SetOriginalRowVersion(currency, request.RowVersion);
        }

        currency.Name = request.Name.Trim();
        currency.DecimalPlaces = request.DecimalPlaces;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> ListRatesAsync(int take = 100, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.CurrenciesManage, ct);
        return await db.ExchangeRates.AsNoTracking().OrderByDescending(r => r.Date).ThenBy(r => r.FromCode).Take(Math.Clamp(take, 1, 500))
            .Select(r => new ExchangeRateDto(r.Id, r.FromCode, r.ToCode, r.Date, r.Rate))
            .ToListAsync(ct);
    }

    /// <summary>Records the rate for a pair and day, replacing an earlier entry for the same day.</summary>
    public async Task SetRateAsync(ExchangeRateRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.CurrenciesManage, ct);
        var from = request.FromCode?.Trim().ToUpperInvariant();
        var to = request.ToCode?.Trim().ToUpperInvariant();
        var v = new Validator().Range(nameof(request.Rate), request.Rate, 0.00000001m, 1_000_000_000m)
            .When(from == to, nameof(request.ToCode), "Validation.Duplicate");
        v.When(!await db.Currencies.AnyAsync(c => c.Code == from, ct), nameof(request.FromCode), "Validation.Required");
        v.When(!await db.Currencies.AnyAsync(c => c.Code == to, ct), nameof(request.ToCode), "Validation.Required");
        v.ThrowIfInvalid();

        var rate = await db.ExchangeRates.FirstOrDefaultAsync(r => r.FromCode == from && r.ToCode == to && r.Date == request.Date, ct);
        if (rate is null)
        {
            db.ExchangeRates.Add(new ExchangeRate { FromCode = from!, ToCode = to!, Date = request.Date, Rate = request.Rate });
        }
        else
        {
            rate.Rate = request.Rate;
        }

        await db.SaveChangesAsync(ct);
    }

    private Task<string?> BaseCurrencyAsync(CancellationToken ct) =>
        db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.BaseCurrencyCode).FirstOrDefaultAsync(ct);
}
