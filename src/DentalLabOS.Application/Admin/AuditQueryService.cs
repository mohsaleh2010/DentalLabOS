using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

/// <summary>ADM-25 (lab audit) and PLT-12 (platform audit). Read-only.</summary>
public sealed class AuditQueryService(IApplicationDbContext db, IPermissionService permissions, IIdentityService identity)
{
    public async Task<PagedResult<AuditLogDto>> ListForTenantAsync(AuditQuery q, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.AuditRead, ct);
        return await QueryAsync(db.AuditLogs.AsNoTracking(), q, ct);
    }

    public async Task<PagedResult<AuditLogDto>> ListForPlatformAsync(AuditQuery q, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Platform.AuditRead, ct);
        return await QueryAsync(db.AuditLogs.IgnoreQueryFilters().AsNoTracking().Where(a => a.TenantId == null), q, ct);
    }

    public async Task<IReadOnlyList<AuditLogDto>> RecentForTenantAsync(int take, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.AuditRead, ct);
        var result = await QueryAsync(db.AuditLogs.AsNoTracking(), new AuditQuery(1, take), ct);
        return result.Items;
    }

    private async Task<PagedResult<AuditLogDto>> QueryAsync(IQueryable<AuditLog> query, AuditQuery q, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(q.Action))
        {
            query = query.Where(a => a.Action == q.Action);
        }

        if (!string.IsNullOrWhiteSpace(q.EntityType))
        {
            query = query.Where(a => a.EntityType == q.EntityType);
        }

        if (q.From is { } from)
        {
            var f = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.CreatedAtUtc >= f);
        }

        if (q.To is { } to)
        {
            var t = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.CreatedAtUtc < t);
        }

        var page = new PageQuery(q.Page, q.PageSize);
        var paged = await query.OrderByDescending(a => a.CreatedAtUtc).ToPagedAsync(page, ct);
        var users = await identity.GetSummariesAsync(paged.Items.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct(), ct);
        var items = paged.Items.Select(a => new AuditLogDto(a.Id, a.CreatedAtUtc,
            a.UserId is { } uid && users.TryGetValue(uid, out var u) ? u.FullName : null,
            a.Action, a.EntityType, a.EntityId, a.ChangesJson, a.Reason, a.IpAddress)).ToList();
        return new PagedResult<AuditLogDto>(items, paged.Page, paged.PageSize, paged.TotalCount);
    }
}
