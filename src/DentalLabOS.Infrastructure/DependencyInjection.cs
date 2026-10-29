using DentalLabOS.Application;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Infrastructure.Security;
using DentalLabOS.Infrastructure.Services;
using DentalLabOS.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalLabOS.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DentalLabOS";

    /// <summary>Registers persistence, tenancy, identity core and application services shared by Web and Api.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName} (environment) or use dotnet user-secrets.");
        }

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<PermissionService>();
        services.AddScoped<IPermissionService>(sp => sp.GetRequiredService<PermissionService>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddSingleton<ISpreadsheetService, ClosedXmlSpreadsheetService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
        services.AddScoped<IBranchSelection, HttpBranchSelection>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<DbInitializer>();
        services.AddSingleton<SetupTokenProvider>();
        services.AddScoped<PlatformBootstrapService>();

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireDigit = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<AppSignInManager>()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.AddApplication();
        return services;
    }
}
