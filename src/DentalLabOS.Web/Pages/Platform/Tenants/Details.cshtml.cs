using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Platform.Tenants;

[Screen("PLT-04")]
[HasPermission(Permissions.Platform.TenantsRead)]
public sealed class DetailsModel(PlatformTenantService tenants) : AppPageModel
{
    public TenantDetailsDto Tenant { get; private set; } = default!;

    public string? Reason { get; set; }

    public async Task OnGetAsync(Guid id) => Tenant = await tenants.GetTenantAsync(id);

    /// <summary>PLT-05: suspend/activate without deleting data.</summary>
    public async Task<IActionResult> OnPostStatusAsync(Guid id, bool activate, string? reason, string rowVersion)
    {
        if (await TryExecuteAsync(() => tenants.ChangeStatusAsync(id, new ChangeTenantStatusRequest(activate, reason, rowVersion)), prefix: string.Empty))
        {
            Flash(activate ? "Platform.Tenant.Activated" : "Platform.Tenant.Suspended");
            return RedirectToPage(new { id });
        }

        Tenant = await tenants.GetTenantAsync(id);
        return Page();
    }
}
