namespace DentalLabOS.Domain.Catalog;

public enum PriceSource { CustomerPrice, CustomerPriceList, DefaultPriceList, ItemDefault }

public sealed record ResolvedPrice(decimal Price, PriceSource Source, Guid? PriceListId);

/// <summary>
/// Price priority (plan A2.2): customer-specific price ← the customer's price list (or its group's)
/// ← the lab's general list ← the item's default sales price. The resolved price is copied into the
/// document line and never follows later list changes.
/// </summary>
public static class PriceResolver
{
    public static ResolvedPrice Resolve(
        Item item,
        decimal quantity,
        DateOnly date,
        IEnumerable<CustomerItemPrice> customerPrices,
        PriceList? customerList,
        PriceList? defaultList)
    {
        var special = customerPrices
            .Where(p => p.ItemId == item.Id && p.IsValidOn(date))
            .OrderByDescending(p => p.ValidFrom ?? DateOnly.MinValue)
            .FirstOrDefault();
        if (special is not null)
        {
            return new(special.Price, PriceSource.CustomerPrice, null);
        }

        if (FromList(customerList, item.Id, quantity, date) is { } fromCustomerList)
        {
            return new(fromCustomerList, PriceSource.CustomerPriceList, customerList!.Id);
        }

        if (FromList(defaultList, item.Id, quantity, date) is { } fromDefault)
        {
            return new(fromDefault, PriceSource.DefaultPriceList, defaultList!.Id);
        }

        return new(item.DefaultSalesPrice, PriceSource.ItemDefault, null);
    }

    /// <summary>The line with the highest minimum quantity that the requested quantity reaches.</summary>
    private static decimal? FromList(PriceList? list, Guid itemId, decimal quantity, DateOnly date)
    {
        if (list is null || !list.IsValidOn(date))
        {
            return null;
        }

        return list.Lines
            .Where(l => l.ItemId == itemId && l.MinQty <= Math.Max(quantity, 1))
            .OrderByDescending(l => l.MinQty)
            .Select(l => (decimal?)l.Price)
            .FirstOrDefault();
    }

    /// <summary>Applies a percentage change and rounds to the given decimals (bulk price update, ITM-09).</summary>
    public static decimal AdjustByPercent(decimal price, decimal percent, int decimals = 2) =>
        Math.Round(price * (1 + (percent / 100m)), decimals, MidpointRounding.AwayFromZero);
}
