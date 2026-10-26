using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;

namespace DentalLabOS.UnitTests;

public sealed class PriceResolverTests
{
    private static readonly DateOnly Today = new(2026, 10, 26);
    private readonly Item item = new() { Id = Guid.NewGuid(), Sku = "ITM-00001", Name = "Zirconia crown", DefaultSalesPrice = 100 };

    private PriceList List(params (decimal MinQty, decimal Price)[] lines)
    {
        var list = new PriceList { Id = Guid.NewGuid(), Name = "L", CurrencyCode = "EGP", IsActive = true };
        foreach (var (minQty, price) in lines)
        {
            list.Lines.Add(new PriceListLine { ItemId = item.Id, MinQty = minQty, Price = price });
        }

        return list;
    }

    [Fact]
    public void Customer_price_wins_over_every_list()
    {
        var special = new CustomerItemPrice { ItemId = item.Id, Price = 70 };
        var result = PriceResolver.Resolve(item, 1, Today, [special], List((1, 80)), List((1, 90)));
        Assert.Equal(new ResolvedPrice(70, PriceSource.CustomerPrice, null), result);
    }

    [Fact]
    public void Expired_customer_price_is_ignored()
    {
        var special = new CustomerItemPrice { ItemId = item.Id, Price = 70, ValidTo = Today.AddDays(-1) };
        var customerList = List((1, 80));
        var result = PriceResolver.Resolve(item, 1, Today, [special], customerList, null);
        Assert.Equal(PriceSource.CustomerPriceList, result.Source);
        Assert.Equal(80, result.Price);
        Assert.Equal(customerList.Id, result.PriceListId);
    }

    [Fact]
    public void Default_list_then_item_price_are_fallbacks()
    {
        var defaultList = List((1, 90));
        Assert.Equal(90, PriceResolver.Resolve(item, 1, Today, [], List(), defaultList).Price);
        Assert.Equal(new ResolvedPrice(100, PriceSource.ItemDefault, null), PriceResolver.Resolve(item, 1, Today, [], null, null));
    }

    [Fact]
    public void Inactive_or_out_of_date_list_is_skipped()
    {
        var inactive = List((1, 80));
        inactive.IsActive = false;
        var future = List((1, 85));
        future.ValidFrom = Today.AddDays(1);
        Assert.Equal(PriceSource.ItemDefault, PriceResolver.Resolve(item, 1, Today, [], inactive, future).Source);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(4, 100)]
    [InlineData(5, 90)]
    [InlineData(20, 75)]
    public void Highest_reached_quantity_tier_applies(decimal qty, decimal expected)
    {
        var list = List((1, 100), (5, 90), (10, 75));
        Assert.Equal(expected, PriceResolver.Resolve(item, qty, Today, [], list, null).Price);
    }

    [Theory]
    [InlineData(100, 10, 110)]
    [InlineData(100, -15, 85)]
    [InlineData(33.33, 10, 36.66)]
    [InlineData(0.125, 0, 0.13)]
    public void Adjust_by_percent_rounds_away_from_zero(decimal price, decimal percent, decimal expected) =>
        Assert.Equal(expected, PriceResolver.AdjustByPercent(price, percent));
}

public sealed class CatalogRuleTests
{
    [Fact]
    public void Gross_quantity_includes_scrap()
    {
        var line = new BomLine { Quantity = 2, ScrapPercent = 10 };
        Assert.Equal(2.2m, line.GrossQuantity);
    }

    [Theory]
    [InlineData(ItemType.Material, true)]
    [InlineData(ItemType.Consumable, true)]
    [InlineData(ItemType.FinishedGood, true)]
    [InlineData(ItemType.Service, false)]
    [InlineData(ItemType.OutsourcedService, false)]
    public void Only_physical_items_are_stock_items(ItemType type, bool stock)
    {
        Assert.Equal(stock, Item.IsStockType(type));
        Assert.Equal(!stock, Item.IsServiceType(type));
    }

    [Theory]
    [InlineData("Clinic", true)]
    [InlineData(" dentist ", true)]
    [InlineData("1", false)]
    [InlineData("99", false)]
    [InlineData("", false)]
    [InlineData("Hospital", false)]
    public void Enums_parse_by_name_only(string value, bool ok) =>
        Assert.Equal(ok, MasterData.TryParseEnum<PartyType>(value, out _));

    [Theory]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("١٢٥٠٫٥", 1250.5)]
    [InlineData("۳۰", 30)]
    [InlineData("-5", -5)]
    public void Import_numbers_accept_arabic_digits(string raw, decimal expected) =>
        Assert.Equal(expected, ImportService.ParseNumber(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("abc")]
    public void Import_rejects_non_numbers(string? raw) => Assert.Null(ImportService.ParseNumber(raw));

    [Theory]
    [InlineData("عيادة", PartyType.Clinic)]
    [InlineData("Center", PartyType.Center)]
    public void Import_party_types_accept_arabic(string raw, PartyType expected) =>
        Assert.Equal(expected, ImportService.ParsePartyType(raw));

    [Theory]
    [InlineData("خدمة خارجية", ItemType.OutsourcedService)]
    [InlineData("Finished good", ItemType.FinishedGood)]
    [InlineData("finishedgood", ItemType.FinishedGood)]
    [InlineData("مستهلك", ItemType.Consumable)]
    public void Import_item_types_accept_aliases(string raw, ItemType expected) =>
        Assert.Equal(expected, ImportService.ParseItemType(raw));

    [Fact]
    public void Unknown_item_type_is_rejected() => Assert.Null(ImportService.ParseItemType("Gadget"));
}
