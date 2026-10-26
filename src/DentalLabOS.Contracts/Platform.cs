namespace DentalLabOS.Contracts.Platform;

public sealed record PlanDto(Guid Id, string Code, string Name, decimal MonthlyPrice, decimal YearlyPrice, int MaxUsers, int MaxBranches,
    int MaxMonthlyWorkOrders, long MaxStorageBytes, bool IncludesAccounting, bool IncludesPortal, bool IncludesAi, bool IsActive, int TenantCount);

public sealed record TenantSummaryDto(Guid Id, string Name, string Slug, string Status, string? PlanName, DateOnly? SubscriptionEndsOn, int UserCount, DateTime CreatedAtUtc);

public sealed record TenantDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string DefaultCulture,
    string BaseCurrencyCode,
    string TimeZoneId,
    string? ContactEmail,
    string? ContactPhone,
    DateTime CreatedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspendReason,
    string? PlanName,
    DateOnly? SubscriptionStart,
    DateOnly? SubscriptionEnd,
    int UserCount,
    int MaxUsers,
    int BranchCount,
    int MaxBranches,
    IReadOnlyList<SubscriptionHistoryDto> SubscriptionHistory,
    string RowVersion);

public sealed record SubscriptionHistoryDto(string PlanName, DateOnly StartDate, DateOnly EndDate, bool IsCurrent, string? ChangeReason);

public sealed record CreateTenantRequest(
    string Name,
    string Slug,
    string OwnerFullName,
    string OwnerEmail,
    Guid PlanId,
    int TrialDays,
    string DefaultCulture = "ar-EG",
    string BaseCurrencyCode = "EGP",
    string TimeZoneId = "Africa/Cairo",
    string? ContactPhone = null,
    string MainBranchName = "الفرع الرئيسي");

public sealed record CreateTenantResult(Guid TenantId, Guid OwnerInvitationId, string OwnerInviteToken, DateTime InviteExpiresAtUtc);

public sealed record ChangeTenantStatusRequest(bool Activate, string? Reason, string RowVersion);

public sealed record PlatformDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int SuspendedTenants,
    int TotalUsers,
    decimal EstimatedMrr,
    IReadOnlyList<TenantSummaryDto> RecentTenants,
    IReadOnlyList<PlanBreakdownDto> Plans);

public sealed record PlanBreakdownDto(string PlanName, int Tenants);
