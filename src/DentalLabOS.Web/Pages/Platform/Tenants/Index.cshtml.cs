using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Platform.Tenants;

[Screen("PLT-02")]
[HasPermission(Permissions.Platform.TenantsRead)]
public sealed class IndexModel(PlatformTenantService tenants) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public TenantStatus? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<TenantSummaryDto> Result { get; private set; } = default!;

    public string PageUrl => $"/platform/tenants?q={Uri.EscapeDataString(Q ?? string.Empty)}&status={Status}&page={{page}}";

    public async Task OnGetAsync() => Result = await tenants.ListTenantsAsync(new PageQuery(PageNumber, 20, Q), Status);
}
