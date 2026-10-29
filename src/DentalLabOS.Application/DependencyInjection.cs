using DentalLabOS.Application.Admin;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Application.Common;
using DentalLabOS.Application.Organization;
using DentalLabOS.Application.Parties;
using DentalLabOS.Application.Production;
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
        services.AddScoped<CustomerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ItemService>();
        services.AddScoped<PriceListService>();
        services.AddScoped<ReferenceDataService>();
        services.AddScoped<ImportService>();
        services.AddScoped<LabCalendar>();
        services.AddScoped<WorkflowService>();
        services.AddScoped<QualityService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<ProductionSetupService>();
        services.AddScoped<WorkOrderService>();
        services.AddScoped<OrderService>();
        services.AddScoped<ProductionService>();
        return services;
    }
}
