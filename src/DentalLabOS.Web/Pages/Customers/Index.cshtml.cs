using DentalLabOS.Application.Parties;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Parties;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Customers;

[Screen("CUS-01")]
[HasPermission(Permissions.Customers.Read)]
public sealed class IndexModel(CustomerService customers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Group { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<CustomerListItemDto> Result { get; private set; } = default!;
    public IReadOnlyList<CustomerGroupDto> Groups { get; private set; } = [];

    public string PageUrl =>
        $"/customers?q={Uri.EscapeDataString(Q ?? string.Empty)}&type={Type}&group={Group}&status={Status}&page={{page}}";

    public async Task OnGetAsync()
    {
        bool? active = Status switch { "active" => true, "inactive" => false, _ => null };
        Result = await customers.ListAsync(new PageQuery(PageNumber, 20, Q), new CustomerFilter(active, Group, Type));
        Groups = await customers.ListGroupsAsync();
    }
}
