using System.Linq.Expressions;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Platform;
using DentalLabOS.Domain.Production;
using DentalLabOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : IdentityUserContext<ApplicationUser, Guid>(options), IApplicationDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<TenantFeatureFlag> TenantFeatureFlags => Set<TenantFeatureFlag>();
    public DbSet<SupportAccessGrant> SupportAccessGrants => Set<SupportAccessGrant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserBranchScope> UserBranchScopes => Set<UserBranchScope>();
    public DbSet<UserDepartmentScope> UserDepartmentScopes => Set<UserDepartmentScope>();
    public DbSet<TechnicianProfile> TechnicianProfiles => Set<TechnicianProfile>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<Dentist> Dentists => Set<Dentist>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierItem> SupplierItems => Set<SupplierItem>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<UnitConversion> UnitConversions => Set<UnitConversion>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListLine> PriceListLines => Set<PriceListLine>();
    public DbSet<CustomerItemPrice> CustomerItemPrices => Set<CustomerItemPrice>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderTooth> WorkOrderTeeth => Set<WorkOrderTooth>();
    public DbSet<WorkOrderRevision> WorkOrderRevisions => Set<WorkOrderRevision>();
    public DbSet<WorkOrderAttachment> WorkOrderAttachments => Set<WorkOrderAttachment>();
    public DbSet<WorkOrderNote> WorkOrderNotes => Set<WorkOrderNote>();
    public DbSet<FileMetadata> Files => Set<FileMetadata>();
    public DbSet<WorkflowDefinition> Workflows => Set<WorkflowDefinition>();
    public DbSet<WorkflowStageDefinition> WorkflowStages => Set<WorkflowStageDefinition>();
    public DbSet<WorkOrderStage> WorkOrderStages => Set<WorkOrderStage>();
    public DbSet<StageTransitionLog> StageTransitions => Set<StageTransitionLog>();
    public DbSet<TaskTimeEntry> TimeEntries => Set<TaskTimeEntry>();
    public DbSet<HoldRecord> Holds => Set<HoldRecord>();
    public DbSet<ReasonCode> ReasonCodes => Set<ReasonCode>();

    /// <summary>
    /// Value used by every tenant query filter. With no tenant in context it is <see cref="Guid.Empty"/>, which matches
    /// nothing — so a missing context fails closed instead of exposing other labs' rows.
    /// </summary>
    internal Guid CurrentTenantFilterId => tenantContext.TenantId ?? Guid.Empty;

    internal ITenantContext TenantContext => tenantContext;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums as strings (ADR-0004): readable in SQL, safe to reorder.
        foreach (var enumType in typeof(Entity).Assembly.GetTypes().Where(t => t.IsEnum))
        {
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(32);
        }

        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (typeof(Entity).IsAssignableFrom(clr) && entityType.BaseType is null)
            {
                builder.Entity(clr).Property(nameof(Entity.RowVersion)).IsRowVersion();
                builder.Entity(clr).Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clr) && entityType.BaseType is null)
            {
                builder.Entity(clr).HasIndex(nameof(ITenantOwned.TenantId));
                builder.Entity(clr).HasQueryFilter(BuildTenantFilter(clr));
            }
        }

        // Audit rows of a lab are visible only inside that lab; platform rows (TenantId null) via platform services.
        builder.Entity<AuditLog>().HasQueryFilter(a => a.TenantId != null && a.TenantId == CurrentTenantFilterId);
    }

    private LambdaExpression BuildTenantFilter(Type clr)
    {
        var e = Expression.Parameter(clr, "e");
        var tenantId = Expression.Property(e, nameof(ITenantOwned.TenantId));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantFilterId));
        Expression body = Expression.Equal(tenantId, current);

        if (typeof(ISoftDeletable).IsAssignableFrom(clr))
        {
            body = Expression.AndAlso(body, Expression.Not(Expression.Property(e, nameof(ISoftDeletable.IsDeleted))));
        }

        return Expression.Lambda(body, e);
    }

    public void SetOriginalRowVersion(Entity entity, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ConcurrencyException();
        }

        Entry(entity).Property(nameof(Entity.RowVersion)).OriginalValue = bytes;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var tx = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await tx.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
