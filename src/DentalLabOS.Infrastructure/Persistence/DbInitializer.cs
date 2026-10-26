using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Platform;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalLabOS.Infrastructure.Persistence;

/// <summary>
/// Applies migrations (only when explicitly enabled — production deployments run them as a reviewed step, plan 6.6)
/// and keeps reference data in sync: the permission catalog and the default subscription plans.
/// </summary>
public sealed class DbInitializer(AppDbContext db, ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync(bool applyMigrations, CancellationToken ct = default)
    {
        if (applyMigrations)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        await SyncPermissionsAsync(ct);
        await SeedPlansAsync(ct);
        await BackfillTenantDefaultsAsync(ct);
    }

    /// <summary>Labs created before a module shipped get that module's starter data (units, currency, numbering...).</summary>
    private async Task BackfillTenantDefaultsAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants.AsNoTracking().Select(t => new { t.Id, t.DefaultCulture, t.BaseCurrencyCode }).ToListAsync(ct);
        var year = DateTime.UtcNow.Year;
        foreach (var t in tenants)
        {
            using (db.TenantContext.BeginScope(t.Id))
            {
                if (await TenantCatalogDefaults.BackfillAsync(db, t.DefaultCulture, t.BaseCurrencyCode, year, ct))
                {
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Added starter catalog data for tenant {TenantId}", t.Id);
                }
            }
        }
    }

    private async Task SyncPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        var added = 0;
        foreach (var key in Permissions.All.Distinct())
        {
            if (!existing.ContainsKey(key))
            {
                db.Permissions.Add(new Permission { Key = key, Module = Permissions.ModuleOf(key), Description = key });
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Added {Count} permissions to the catalog", added);
        }
    }

    private async Task SeedPlansAsync(CancellationToken ct)
    {
        if (await db.SubscriptionPlans.AnyAsync(ct))
        {
            return;
        }

        // Placeholder prices: final pricing is decided after market interviews (plan section 13).
        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                Code = "starter", Name = "Starter", MonthlyPrice = 0, YearlyPrice = 0, MaxUsers = 10, MaxBranches = 1,
                MaxMonthlyWorkOrders = 500, MaxStorageBytes = 5L * 1024 * 1024 * 1024, SortOrder = 1,
            },
            new SubscriptionPlan
            {
                Code = "professional", Name = "Professional", MonthlyPrice = 0, YearlyPrice = 0, MaxUsers = 40, MaxBranches = 3,
                MaxMonthlyWorkOrders = 3000, MaxStorageBytes = 50L * 1024 * 1024 * 1024, IncludesAccounting = true, IncludesPortal = true,
                SortOrder = 2,
            },
            new SubscriptionPlan
            {
                Code = "enterprise", Name = "Enterprise", MonthlyPrice = 0, YearlyPrice = 0, MaxUsers = 0, MaxBranches = 0,
                MaxMonthlyWorkOrders = 0, MaxStorageBytes = 0, IncludesAccounting = true, IncludesPortal = true, IncludesAi = true, SortOrder = 3,
            });
        await db.SaveChangesAsync(ct);
    }
}
