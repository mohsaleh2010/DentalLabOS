using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Customers;

[Screen("CUS-03")]
[HasPermission(Permissions.Customers.Read)]
public sealed class DetailsModel(CustomerService customers, ItemService items) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "info";

    [BindProperty] public PriceInput NewPrice { get; set; } = new();

    public CustomerDto Customer { get; private set; } = default!;
    public IReadOnlyList<DentistDto> Dentists { get; private set; } = [];
    public IReadOnlyList<CustomerPriceDto> Prices { get; private set; } = [];
    public IReadOnlyList<OptionDto> ItemOptions { get; private set; } = [];

    public sealed class PriceInput
    {
        public Guid ItemId { get; set; }
        public decimal Price { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddPriceAsync()
    {
        Tab = "prices";
        var ok = await TryExecuteAsync(() => customers.AddPriceAsync(Id, new CustomerPriceUpsertRequest(NewPrice.ItemId, NewPrice.Price, NewPrice.ValidFrom, NewPrice.ValidTo)), "NewPrice");
        if (!ok)
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/customers/{Id}?tab=prices");
    }

    public async Task<IActionResult> OnPostDeletePriceAsync(Guid priceId)
    {
        await customers.DeletePriceAsync(Id, priceId);
        Flash("Common.Deleted");
        return Redirect($"/customers/{Id}?tab=prices");
    }

    private async Task LoadAsync()
    {
        Customer = await customers.GetAsync(Id);
        Tab = Tab is "dentists" or "prices" ? Tab : "info";
        Dentists = await customers.ListDentistsAsync(Id);
        if (Tab == "prices" && Can(Permissions.PriceLists.Read))
        {
            Prices = await customers.ListPricesAsync(Id);
            if (Can(Permissions.PriceLists.Manage) && Can(Permissions.Items.Read))
            {
                ItemOptions = await items.OptionsAsync(null, "service");
            }
        }
    }
}
