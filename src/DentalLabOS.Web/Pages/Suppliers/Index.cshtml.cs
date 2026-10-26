using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Suppliers;

[Screen("SUP-01")]
[HasPermission(Permissions.Suppliers.Read)]
public sealed class IndexModel(SupplierService suppliers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<SupplierListItemDto> Result { get; private set; } = default!;

    public string PageUrl => $"/suppliers?q={Uri.EscapeDataString(Q ?? string.Empty)}&status={Status}&page={{page}}";

    public async Task OnGetAsync()
    {
        bool? active = Status switch { "active" => true, "inactive" => false, _ => null };
        Result = await suppliers.ListAsync(new PageQuery(PageNumber, 20, Q), active);
    }
}
