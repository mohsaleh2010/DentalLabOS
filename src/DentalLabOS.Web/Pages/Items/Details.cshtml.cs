using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-03")]
[HasPermission(Permissions.Items.Read)]
public sealed class DetailsModel(ItemService items, SupplierService suppliers, PriceListService priceLists) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "info";

    public ItemDto Item { get; private set; } = default!;
    public bool IsService => Item.Type is "Service" or "OutsourcedService";
    public IReadOnlyList<BomLineDto> Bom { get; private set; } = [];
    public IReadOnlyList<SupplierItemDto> Suppliers { get; private set; } = [];
    public IReadOnlyList<ItemPriceDto> Prices { get; private set; } = [];
    public IReadOnlyList<UnitConversionDto> Conversions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Item = await items.GetAsync(Id);
        Tab = Tab switch
        {
            "bom" when IsService => "bom",
            "suppliers" when !IsService => "suppliers",
            "prices" when Can(Permissions.PriceLists.Read) => "prices",
            _ => "info",
        };

        switch (Tab)
        {
            case "bom":
                Bom = await items.ListBomAsync(Id);
                break;
            case "suppliers":
                Suppliers = await suppliers.ListForItemAsync(Id);
                break;
            case "prices":
                Prices = await priceLists.ListItemPricesAsync(Id);
                break;
            default:
                Conversions = await items.ListConversionsAsync(Id);
                break;
        }
    }
}
