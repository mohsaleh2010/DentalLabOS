using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Platform;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Platform;

/// <summary>
/// Platform portal (PLT-01..06). This is one of the few services allowed to bypass tenant query filters,
/// and it only reads tenant metadata and counts — never case content (plan 8.1, platform admin rules).
/// </summary>
public sealed class PlatformTenantService(
    IApplicationDbContext db,
    IPermissionService permissions,
    ITenantContext tenantContext,
    IIdentityService identity,
    IAuditWriter audit,
    IClock clock)
{
    public const string SlugPattern = "^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$";

    public async Task<PlatformDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.DashboardRead, ct);

        var statusCounts = await db.Tenants.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalUsers = await db.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.IsActive, ct);

        var mrr = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.IsCurrent && s.Tenant.Status == TenantStatus.Active)
            .SumAsync(s => (decimal?)s.Plan.MonthlyPrice, ct) ?? 0m;

        var plans = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.IsCurrent)
            .GroupBy(s => s.Plan.Name)
            .Select(g => new PlanBreakdownDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var recent = await ListTenantsAsync(new PageQuery(1, 5), null, ct);

        int Count(TenantStatus s) => statusCounts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;

        return new PlatformDashboardDto(
            statusCounts.Sum(c => c.Count),
            Count(TenantStatus.Active),
            Count(TenantStatus.Trial),
            Count(TenantStatus.Suspended),
            totalUsers,
            mrr,
            recent.Items,
            plans);
    }

    public async Task<PagedResult<TenantSummaryDto>> ListTenantsAsync(PageQuery page, TenantStatus? status, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.TenantsRead, ct);

        var query = db.Tenants.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }

        if (page.NormalizedSearch() is { } search)
        {
            query = query.Where(t => t.Name.Contains(search) || t.Slug.Contains(search));
        }

        query = page.Sort switch
        {
            "name" => query.OrderBy(t => t.Name),
            _ => query.OrderByDescending(t => t.CreatedAtUtc),
        };

        return await query.Select(t => new TenantSummaryDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Status.ToString(),
                t.Subscriptions.Where(s => s.IsCurrent).Select(s => s.Plan.Name).FirstOrDefault(),
                t.Subscriptions.Where(s => s.IsCurrent).Select(s => (DateOnly?)s.EndDate).FirstOrDefault(),
                db.UserProfiles.IgnoreQueryFilters().Count(u => u.TenantId == t.Id && u.IsActive),
                t.CreatedAtUtc))
            .ToPagedAsync(page, ct);
    }

    public async Task<TenantDetailsDto> GetTenantAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.TenantsRead, ct);

        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Tenant));
        var history = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == id)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new { s.Plan.Name, s.StartDate, s.EndDate, s.IsCurrent, s.ChangeReason, s.Plan.MaxUsers, s.Plan.MaxBranches })
            .ToListAsync(ct);
        var current = history.FirstOrDefault(h => h.IsCurrent);
        var users = await db.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.TenantId == id && u.IsActive, ct);
        var branches = await db.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == id && b.IsActive, ct);

        return new TenantDetailsDto(
            t.Id, t.Name, t.Slug, t.Status.ToString(), t.DefaultCulture, t.BaseCurrencyCode, t.TimeZoneId,
            t.ContactEmail, t.ContactPhone, t.CreatedAtUtc, t.SuspendedAtUtc, t.SuspendReason,
            current?.Name, current?.StartDate, current?.EndDate,
            users, current?.MaxUsers ?? 0, branches, current?.MaxBranches ?? 0,
            history.Select(h => new SubscriptionHistoryDto(h.Name, h.StartDate, h.EndDate, h.IsCurrent, h.ChangeReason)).ToList(),
            RowVersions.Encode(t.RowVersion));
    }

    public async Task ChangeStatusAsync(Guid id, ChangeTenantStatusRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.TenantsManage, ct);

        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Tenant));
        db.SetOriginalRowVersion(tenant, request.RowVersion);
        var before = tenant.Status;

        try
        {
            if (request.Activate)
            {
                tenant.Activate();
            }
            else
            {
                tenant.Suspend(request.Reason ?? string.Empty, clock.UtcNow);
            }
        }
        catch (DomainException ex)
        {
            throw ValidationException.For(nameof(request.Reason), ex.MessageKey);
        }

        audit.Add(AuditActions.StatusChange, nameof(Tenant), tenant.Id, request.Reason, new { from = before.ToString(), to = tenant.Status.ToString() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(bool activeOnly, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.TenantsRead, ct);

        return await db.SubscriptionPlans.AsNoTracking()
            .Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanDto(p.Id, p.Code, p.Name, p.MonthlyPrice, p.YearlyPrice, p.MaxUsers, p.MaxBranches,
                p.MaxMonthlyWorkOrders, p.MaxStorageBytes, p.IncludesAccounting, p.IncludesPortal, p.IncludesAi, p.IsActive,
                db.TenantSubscriptions.Count(s => s.PlanId == p.Id && s.IsCurrent)))
            .ToListAsync(ct);
    }

    /// <summary>PLT-03: provisions a lab with its main branch, built-in roles, numbering, starter catalog data and an owner invitation.</summary>
    public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.TenantsCreate, ct);

        var slug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;
        var email = request.OwnerEmail?.Trim() ?? string.Empty;
        var v = new Validator()
            .Required(nameof(request.Name), request.Name).MaxLength(nameof(request.Name), request.Name, 200)
            .Required(nameof(request.Slug), slug).Pattern(nameof(request.Slug), slug, SlugPattern, "Validation.Slug")
            .Required(nameof(request.OwnerFullName), request.OwnerFullName).MaxLength(nameof(request.OwnerFullName), request.OwnerFullName, 200)
            .Required(nameof(request.OwnerEmail), email).Email(nameof(request.OwnerEmail), email)
            .Range(nameof(request.TrialDays), request.TrialDays, 0, 90);

        if (!string.IsNullOrEmpty(slug) && await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            v.Add(nameof(request.Slug), "Validation.SlugTaken");
        }

        if (!string.IsNullOrEmpty(email) && await identity.EmailExistsAsync(email, ct))
        {
            v.Add(nameof(request.OwnerEmail), "Validation.EmailTaken");
        }

        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);
        v.When(plan is null, nameof(request.PlanId), "Validation.Required");
        v.ThrowIfInvalid();

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var token = SecureToken.Create();

        return await db.InTransactionAsync(async innerCt =>
        {
            var tenant = new Tenant
            {
                Name = request.Name.Trim(),
                Slug = slug,
                Status = request.TrialDays > 0 ? TenantStatus.Trial : TenantStatus.Active,
                DefaultCulture = request.DefaultCulture,
                BaseCurrencyCode = request.BaseCurrencyCode,
                TimeZoneId = request.TimeZoneId,
                ContactEmail = email,
                ContactPhone = request.ContactPhone,
                CreatedAtUtc = now,
            };
            db.Tenants.Add(tenant);
            db.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = tenant.Id,
                PlanId = plan!.Id,
                StartDate = today,
                EndDate = request.TrialDays > 0 ? today.AddDays(request.TrialDays) : today.AddYears(1),
                IsCurrent = true,
                ChangeReason = request.TrialDays > 0 ? "Trial" : "Initial",
                CreatedAtUtc = now,
            });
            audit.Add(AuditActions.Create, nameof(Tenant), tenant.Id, null, new { tenant.Name, tenant.Slug, plan = plan.Code });

            Guid invitationId;
            using (tenantContext.BeginScope(tenant.Id))
            {
                db.Branches.Add(new Branch { Code = "MAIN", Name = request.MainBranchName, IsMain = true });

                var permissionIds = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Key, p => p.Id, innerCt);
                AppRole? ownerRole = null;
                foreach (var def in SystemRoles.All)
                {
                    var role = new AppRole
                    {
                        Name = SystemRoleNames.Get(def.Key, request.DefaultCulture),
                        SystemKey = def.Key,
                        IsSystemRole = true,
                    };
                    db.Roles.Add(role);
                    foreach (var key in def.Permissions.Distinct())
                    {
                        if (permissionIds.TryGetValue(key, out var pid))
                        {
                            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
                        }
                    }

                    if (def.Key == SystemRoles.Owner)
                    {
                        ownerRole = role;
                    }
                }

                foreach (var (type, prefix) in DefaultSequences.All)
                {
                    db.DocumentSequences.Add(new DocumentSequence { DocumentType = type, Prefix = prefix, Year = today.Year });
                }

                TenantCatalogDefaults.Add(db, request.DefaultCulture, request.BaseCurrencyCode, today.Year);

                var invitation = new UserInvitation
                {
                    Email = email,
                    FullName = request.OwnerFullName.Trim(),
                    RoleId = ownerRole!.Id,
                    TokenHash = SecureToken.Hash(token),
                    ExpiresAtUtc = now.AddDays(7),
                };
                db.UserInvitations.Add(invitation);
                invitationId = invitation.Id;

                await db.SaveChangesAsync(innerCt);
            }

            return new CreateTenantResult(tenant.Id, invitationId, token, now.AddDays(7));
        }, ct);
    }
}
