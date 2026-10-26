using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Suppliers;

[Screen("SUP-03")]
[HasPermission(Permissions.Suppliers.Read)]
public sealed class DetailsModel(SupplierService suppliers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "info";

    public SupplierDto Supplier { get; private set; } = default!;
    public IReadOnlyList<SupplierItemDto> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Supplier = await suppliers.GetAsync(Id);
        Tab = Tab == "items" ? Tab : "info";
        Items = await suppliers.ListItemsAsync(Id);
    }
}
