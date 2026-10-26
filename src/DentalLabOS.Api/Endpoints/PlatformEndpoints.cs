using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Endpoints;

/// <summary>Platform portal (PLT-01..05) for platform staff only.</summary>
public static class PlatformEndpoints
{
    public static RouteGroupBuilder MapPlatformEndpoints(this RouteGroupBuilder api)
    {
        var platform = api.MapGroup("/platform").WithTags("Platform");

        platform.MapGet("/dashboard", (PlatformTenantService svc, CancellationToken ct) => svc.GetDashboardAsync(ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.DashboardRead));

        platform.MapGet("/tenants", (int? page, int? pageSize, string? search, TenantStatus? status, PlatformTenantService svc, CancellationToken ct) =>
                svc.ListTenantsAsync(new PageQuery(page ?? 1, pageSize ?? 20, search), status, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.TenantsRead));

        platform.MapGet("/tenants/{id:guid}", (Guid id, PlatformTenantService svc, CancellationToken ct) => svc.GetTenantAsync(id, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.TenantsRead));

        platform.MapPost("/tenants", (CreateTenantRequest request, PlatformTenantService svc, CancellationToken ct) => svc.CreateTenantAsync(request, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.TenantsCreate))
            .WithSummary("Provision a lab with its main branch, default roles, numbering and an owner invitation");

        platform.MapPost("/tenants/{id:guid}/status", async (Guid id, ChangeTenantStatusRequest request, PlatformTenantService svc, CancellationToken ct) =>
            {
                await svc.ChangeStatusAsync(id, request, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.TenantsManage))
            .WithSummary("Suspend or reactivate a lab (reason required)");

        platform.MapGet("/plans", (PlatformTenantService svc, CancellationToken ct) => svc.ListPlansAsync(false, ct))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Platform.TenantsRead));

        return api;
    }
}
