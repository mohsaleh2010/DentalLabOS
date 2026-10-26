namespace DentalLabOS.Contracts.Parties;

// Enum values travel as their names: Clinic, Dentist, Center, Individual.

public sealed record CustomerListItemDto(
    Guid Id, string Code, string Name, string Type, string? GroupName, string? Phone, string? Mobile, string? City,
    int DentistCount, decimal CreditLimit, bool IsActive);

public sealed record CustomerDto(
    Guid Id, string Code, string Name, string Type, Guid? GroupId, string? GroupName, string? Phone, string? Mobile, string? Email,
    string? Address, string? City, string? TaxNumber, int PaymentTermsDays, decimal CreditLimit, bool BlockWhenOverCredit,
    Guid? PriceListId, string? PriceListName, decimal OpeningBalance, bool IsActive, string? InternalNotes, DateTime CreatedAtUtc,
    string RowVersion);

/// <summary>Leave <see cref="Code"/> empty to get the next number (C-00001).</summary>
public sealed record CustomerUpsertRequest(
    string? Code, string Name, string Type, Guid? GroupId = null, string? Phone = null, string? Mobile = null, string? Email = null,
    string? Address = null, string? City = null, string? TaxNumber = null, int PaymentTermsDays = 0, decimal CreditLimit = 0,
    bool BlockWhenOverCredit = false, Guid? PriceListId = null, decimal OpeningBalance = 0, string? InternalNotes = null,
    bool IsActive = true, string? RowVersion = null);

public sealed record CustomerGroupDto(Guid Id, string Name, Guid? DefaultPriceListId, string? DefaultPriceListName, int CustomerCount, string RowVersion);

public sealed record CustomerGroupUpsertRequest(string Name, Guid? DefaultPriceListId = null, string? RowVersion = null);

public sealed record DentistDto(Guid Id, Guid CustomerId, string FullName, string? Specialty, string? Phone, string? Email, bool IsActive, string RowVersion);

public sealed record DentistUpsertRequest(string FullName, string? Specialty = null, string? Phone = null, string? Email = null, bool IsActive = true, string? RowVersion = null);

public sealed record CustomerPriceDto(Guid Id, Guid ItemId, string ItemSku, string ItemName, decimal Price, decimal DefaultPrice, DateOnly? ValidFrom, DateOnly? ValidTo);

public sealed record CustomerPriceUpsertRequest(Guid ItemId, decimal Price, DateOnly? ValidFrom = null, DateOnly? ValidTo = null);

public sealed record SupplierListItemDto(Guid Id, string Code, string Name, string? Phone, string? Email, decimal? Rating, bool IsOutsourcePartner, int ItemCount, bool IsActive);

public sealed record SupplierDto(
    Guid Id, string Code, string Name, string? Phone, string? Email, string? Address, string? TaxNumber, int PaymentTermsDays,
    decimal OpeningBalance, decimal? Rating, bool IsOutsourcePartner, bool IsActive, DateTime CreatedAtUtc, string RowVersion);

/// <summary>Leave <see cref="Code"/> empty to get the next number (S-00001).</summary>
public sealed record SupplierUpsertRequest(
    string? Code, string Name, string? Phone = null, string? Email = null, string? Address = null, string? TaxNumber = null,
    int PaymentTermsDays = 0, decimal OpeningBalance = 0, decimal? Rating = null, bool IsOutsourcePartner = false,
    bool IsActive = true, string? RowVersion = null);

/// <summary><see cref="LastPrice"/> is null when the caller may not see costs.</summary>
public sealed record SupplierItemDto(
    Guid Id, Guid SupplierId, string SupplierName, Guid ItemId, string ItemSku, string ItemName, string? SupplierSku, decimal? LastPrice,
    string CurrencyCode, int LeadTimeDays, decimal MinOrderQty, bool IsPreferred);

public sealed record SupplierItemUpsertRequest(
    Guid ItemId, string? SupplierSku, decimal LastPrice, string CurrencyCode, int LeadTimeDays = 0, decimal MinOrderQty = 0, bool IsPreferred = false);
