using DentalLabOS.Application.Admin;
using DentalLabOS.Application.Organization;
using DentalLabOS.Application.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DentalLabOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PlatformTenantService>();
        services.AddScoped<BranchService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<RoleService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<NumberingAdminService>();
        services.AddScoped<LabDashboardService>();
        return services;
    }
}
