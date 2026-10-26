using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Platform;
using DentalLabOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalLabOS.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.DefaultCulture).HasMaxLength(10);
        b.Property(x => x.BaseCurrencyCode).HasMaxLength(3);
        b.Property(x => x.TimeZoneId).HasMaxLength(64);
        b.Property(x => x.SuspendReason).HasMaxLength(500);
        b.Ignore(x => x.CanSignIn);
    }
}

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        b.Property(x => x.YearlyPrice).HasPrecision(18, 2);
    }
}

internal sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> b)
    {
        b.HasOne(x => x.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.IsCurrent });
        b.Property(x => x.ChangeReason).HasMaxLength(500);
    }
}

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> b)
    {
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.HasMany(x => x.Tenants).WithOne().HasForeignKey(x => x.FeatureFlagId);
    }
}

internal sealed class TenantFeatureFlagConfiguration : IEntityTypeConfiguration<TenantFeatureFlag>
{
    public void Configure(EntityTypeBuilder<TenantFeatureFlag> b) => b.HasIndex(x => new { x.TenantId, x.FeatureFlagId }).IsUnique();
}

internal sealed class SupportAccessGrantConfiguration : IEntityTypeConfiguration<SupportAccessGrant>
{
    public void Configure(EntityTypeBuilder<SupportAccessGrant> b)
    {
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(64);
        b.HasIndex(x => new { x.TenantId, x.ExpiresAtUtc });
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        b.Property(x => x.ChangesJson).HasMaxLength(-1);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        b.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.PreferredCulture).HasMaxLength(10);
        b.Property(x => x.Theme).HasMaxLength(10);
        b.HasIndex(x => x.TenantId);

        // Global uniqueness of the e-mail used to sign in (one account = one lab, ADR-0003).
        b.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
