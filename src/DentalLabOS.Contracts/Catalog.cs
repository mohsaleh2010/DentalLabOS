namespace DentalLabOS.Contracts.Catalog;

// Item types travel as their names: Service, Material, Consumable, FinishedGood, OutsourcedService.
// Cost fields are null when the caller lacks the cost permission (inventory.valuation.read).

public sealed record OptionDto(Guid Id, string Code, string Name);

public sealed record ItemListItemDto(
    Guid Id, string Sku, string Name, string? NameEn, string Type, string CategoryName, string UnitCode, decimal DefaultSalesPrice,
    decimal? StandardCost, bool IsStockTracked, bool IsActive);

public sealed record ItemDto(
    Guid Id, string Sku, string Name, string? NameEn, string Type, Guid CategoryId, string CategoryName, Guid BaseUnitId, string BaseUnitCode,
    bool IsStockTracked, bool TracksBatches, bool TracksExpiry, decimal ReorderLevel, decimal ReorderQty, decimal? StandardCost,
    decimal DefaultSalesPrice, Guid? TaxCodeId, string? TaxCodeName, decimal StandardLeadHours, decimal DifficultyFactor, string? Barcode,
    bool IsActive, DateTime CreatedAtUtc, string RowVersion);

/// <summary>Leave <see cref="Sku"/> empty to get the next number. A null <see cref="StandardCost"/> keeps the current cost.</summary>
public sealed record ItemUpsertRequest(
    string? Sku, string Name, string Type, Guid CategoryId, Guid BaseUnitId, string? NameEn = null, bool TracksBatches = false,
    bool TracksExpiry = false, decimal ReorderLevel = 0, decimal ReorderQty = 0, decimal? StandardCost = null, decimal DefaultSalesPrice = 0,
    Guid? TaxCodeId = null, decimal StandardLeadHours = 0, decimal DifficultyFactor = 1, string? Barcode = null, bool IsActive = true,
    string? RowVersion = null);

public sealed record ItemCategoryDto(Guid Id, Guid? ParentId, string? ParentName, string Name, string? DefaultType, int ItemCount, string RowVersion);

public sealed record ItemCategoryUpsertRequest(string Name, Guid? ParentId = null, string? DefaultType = null, string? RowVersion = null);

public sealed record BomLineDto(
    Guid Id, Guid MaterialItemId, string MaterialSku, string MaterialName, decimal Quantity, Guid UnitId, string UnitCode,
    decimal ScrapPercent, decimal GrossQuantity, decimal? LineCost);

public sealed record BomLineRequest(Guid MaterialItemId, decimal Quantity, Guid UnitId, decimal ScrapPercent = 0);

public sealed record UnitConversionDto(Guid Id, Guid FromUnitId, string FromUnitCode, Guid ToUnitId, string ToUnitCode, decimal Factor);

public sealed record UnitConversionRequest(Guid FromUnitId, Guid ToUnitId, decimal Factor);

public sealed record UnitDto(Guid Id, string Code, string Name, int DecimalPlaces, int ItemCount, string RowVersion);

public sealed record UnitUpsertRequest(string Code, string Name, int DecimalPlaces = 2, string? RowVersion = null);

public sealed record TaxCodeDto(Guid Id, string Code, string Name, decimal Rate, bool IsWithholding, bool IsActive, string RowVersion);

public sealed record TaxCodeUpsertRequest(string Code, string Name, decimal Rate, bool IsWithholding = false, bool IsActive = true, string? RowVersion = null);

public sealed record CurrencyDto(Guid Id, string Code, string Name, int DecimalPlaces, bool IsBase, string RowVersion);

public sealed record CurrencyUpsertRequest(string Code, string Name, int DecimalPlaces = 2, string? RowVersion = null);

public sealed record ExchangeRateDto(Guid Id, string FromCode, string ToCode, DateOnly Date, decimal Rate);

/// <summary>1 <see cref="FromCode"/> = <see cref="Rate"/> × <see cref="ToCode"/>. One rate per pair and day (re-posting replaces it).</summary>
public sealed record ExchangeRateRequest(string FromCode, string ToCode, DateOnly Date, decimal Rate);

public sealed record PriceListDto(
    Guid Id, string Name, string CurrencyCode, bool IsDefault, DateOnly? ValidFrom, DateOnly? ValidTo, bool IsActive, int LineCount,
    int CustomerCount, string RowVersion);

public sealed record PriceListUpsertRequest(
    string Name, string CurrencyCode, bool IsDefault = false, DateOnly? ValidFrom = null, DateOnly? ValidTo = null, bool IsActive = true,
    string? RowVersion = null);

public sealed record PriceListLineDto(Guid Id, Guid ItemId, string ItemSku, string ItemName, string ItemType, decimal Price, decimal MinQty, decimal DefaultPrice);

public sealed record ItemPriceDto(Guid PriceListId, string PriceListName, bool IsDefault, bool IsActive, decimal MinQty, decimal Price);

public sealed record PriceListLineRequest(Guid ItemId, decimal Price, decimal MinQty = 1);

/// <summary>ITM-09: change every matching line of a list by a percentage. Preview first, then apply.</summary>
public sealed record BulkPriceUpdateRequest(Guid PriceListId, decimal Percent, Guid? CategoryId = null, string? ItemType = null, int Decimals = 2);

public sealed record BulkPriceChangeDto(Guid ItemId, string ItemSku, string ItemName, decimal MinQty, decimal OldPrice, decimal NewPrice);

public sealed record BulkPricePreviewDto(int Count, decimal OldTotal, decimal NewTotal, IReadOnlyList<BulkPriceChangeDto> Changes);

public sealed record PriceQuoteDto(Guid ItemId, Guid CustomerId, decimal Quantity, decimal Price, string Source, Guid? PriceListId, string? PriceListName);

public enum ImportKind { Customers, Items }

public sealed record ImportRowError(int Row, string Column, string MessageKey);

/// <summary>Result of validating a spreadsheet. Nothing is saved unless every row is valid.</summary>
public sealed record ImportPreviewDto(
    string Kind, int TotalRows, int ValidRows, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Sample,
    IReadOnlyList<ImportRowError> Errors)
{
    public bool CanImport => TotalRows > 0 && Errors.Count == 0;
}

public sealed record ImportResultDto(string Kind, int Created);
