using DentalLabOS.Domain.Catalog;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Parties;
using DentalLabOS.Domain.Platform;
using DentalLabOS.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Abstractions;

/// <summary>
/// Persistence boundary used by application services. Tenant-owned sets are automatically filtered to the
/// current tenant by global query filters; bypassing them is restricted to platform services (see TENANT_ISOLATION.md).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Department> Departments { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserInvitation> UserInvitations { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerGroup> CustomerGroups { get; }
    DbSet<CustomerContact> CustomerContacts { get; }
    DbSet<Dentist> Dentists { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierItem> SupplierItems { get; }
    DbSet<ItemCategory> ItemCategories { get; }
    DbSet<UnitOfMeasure> Units { get; }
    DbSet<UnitConversion> UnitConversions { get; }
    DbSet<Item> Items { get; }
    DbSet<BomLine> BomLines { get; }
    DbSet<PriceList> PriceLists { get; }
    DbSet<PriceListLine> PriceListLines { get; }
    DbSet<CustomerItemPrice> CustomerItemPrices { get; }
    DbSet<TaxCode> TaxCodes { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<TechnicianProfile> TechnicianProfiles { get; }
    DbSet<UserDepartmentScope> UserDepartmentScopes { get; }
    DbSet<UserBranchScope> UserBranchScopes { get; }
    DbSet<CustomerOrder> CustomerOrders { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<WorkOrderTooth> WorkOrderTeeth { get; }
    DbSet<WorkOrderRevision> WorkOrderRevisions { get; }
    DbSet<WorkOrderAttachment> WorkOrderAttachments { get; }
    DbSet<WorkOrderNote> WorkOrderNotes { get; }
    DbSet<FileMetadata> Files { get; }
    DbSet<WorkflowDefinition> Workflows { get; }
    DbSet<WorkflowStageDefinition> WorkflowStages { get; }
    DbSet<WorkOrderStage> WorkOrderStages { get; }
    DbSet<StageTransitionLog> StageTransitions { get; }
    DbSet<TaskTimeEntry> TimeEntries { get; }
    DbSet<HoldRecord> Holds { get; }
    DbSet<ReasonCode> ReasonCodes { get; }
    DbSet<QualityTemplate> QualityTemplates { get; }
    DbSet<QualityTemplateItem> QualityTemplateItems { get; }
    DbSet<QualityCheck> QualityChecks { get; }
    DbSet<QualityCheckItem> QualityCheckItems { get; }
    DbSet<CaseAdjustment> Adjustments { get; }
    DbSet<RemakeRecord> Remakes { get; }
    DbSet<DeliveryNote> DeliveryNotes { get; }
    DbSet<DeliveryNoteLine> DeliveryNoteLines { get; }
    DbSet<DeliveryRun> DeliveryRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the original concurrency token so the next save fails if the row changed meanwhile.</summary>
    void SetOriginalRowVersion(Entity entity, string? rowVersion);

    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    PlatformRole PlatformRole { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformStaff => PlatformRole != PlatformRole.None;
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

/// <summary>
/// The tenant every query and write is scoped to. Normally derived from the signed-in user; platform provisioning
/// may switch it explicitly with <see cref="BeginScope"/>.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool IsPlatformContext { get; }
    IDisposable BeginScope(Guid tenantId);
}

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default);
    Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> unless the current user holds the permission.</summary>
    Task DemandAsync(string permission, CancellationToken cancellationToken = default);

    void Invalidate();
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed record IdentityUserSummary(Guid UserId, string Email, string FullName, DateTime? LastLoginAtUtc, bool IsActive);

/// <summary>Boundary to ASP.NET Core Identity (which lives in Infrastructure).</summary>
public interface IIdentityService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> CreateUserAsync(string email, string fullName, string password, Guid? tenantId, PlatformRole platformRole, string culture, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IdentityUserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> AnyPlatformOwnerAsync(CancellationToken cancellationToken = default);
}

public interface IAuditWriter
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the next SaveChanges).</summary>
    void Add(string action, string entityType, Guid? entityId, string? reason = null, object? changes = null, Guid? tenantId = null);
}

public interface INumberingService
{
    /// <summary>Allocates the next document number inside the caller's transaction (gap-free on rollback).</summary>
    Task<string> NextAsync(string documentType, Guid? branchId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>The first worksheet of an uploaded workbook: header row plus data rows as text (numbers in invariant culture).</summary>
public sealed record SheetData(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>Excel boundary (ClosedXML lives in Infrastructure).</summary>
public interface ISpreadsheetService
{
    /// <summary>Reads at most <paramref name="maxRows"/> data rows; throws <see cref="Common.ValidationException"/> for unreadable files.</summary>
    SheetData Read(Stream stream, int maxRows);

    /// <summary>A workbook with one data sheet (bold, frozen header) and an optional help sheet.</summary>
    byte[] CreateTemplate(string sheetName, IReadOnlyList<string> headers, string? helpSheetName, IReadOnlyList<(string Column, string Help)> help);
}

/// <summary>Private file storage (never under wwwroot). Keys are generated by the application, not taken from users.</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Renders QR codes for printed tickets (ORD-26).</summary>
public interface IQrCodeRenderer
{
    /// <summary>A standalone SVG document.</summary>
    string Svg(string text);
}

/// <summary>The branch the user picked (AUTH-08: cookie on the web, X-Branch-Id header on the API). Services validate it.</summary>
public interface IBranchSelection
{
    Guid? RequestedBranchId { get; }
}
