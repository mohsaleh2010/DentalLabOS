using DentalLabOS.Application.Abstractions;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

/// <summary>ADM-01. Production KPIs join this dashboard once work orders exist (phase 3).</summary>
public sealed class LabDashboardService(IApplicationDbContext db, IPermissionService permissions, ITenantContext tenant, IClock clock)
{
    public async Task<LabDashboardDto> GetAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.DashboardRead, ct);
        var tenantId = tenant.TenantId!.Value;
        var now = clock.UtcNow;

        var t = await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == tenantId, ct);
        var sub = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsCurrent)
            .Select(s => new { s.Plan.Name, s.EndDate, s.Plan.MaxUsers, s.Plan.MaxBranches })
            .FirstOrDefaultAsync(ct);

        var activeUsers = await db.UserProfiles.CountAsync(u => u.IsActive, ct);
        var branches = await db.Branches.CountAsync(b => b.IsActive, ct);
        var departments = await db.Departments.CountAsync(d => d.IsActive, ct);
        var invitations = await db.UserInvitations.CountAsync(i => i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct);
        var roles = await db.Roles.CountAsync(ct);

        IReadOnlyList<AuditLogDto> recent = [];
        if (await permissions.HasAsync(Permissions.Admin.AuditRead, ct))
        {
            recent = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).Take(6)
                .Select(a => new AuditLogDto(a.Id, a.CreatedAtUtc, null, a.Action, a.EntityType, a.EntityId, null, a.Reason, null))
                .ToListAsync(ct);
        }

        var steps = new List<SetupStepDto>
        {
            new("Setup.Branches", await db.Branches.AnyAsync(b => b.Phone != null || b.Address != null, ct), "/admin/branches"),
            new("Setup.Departments", departments > 0, "/admin/departments"),
            new("Setup.InviteTeam", activeUsers + invitations > 1, "/admin/users"),
            new("Setup.ReviewRoles", await db.Roles.AnyAsync(r => r.UpdatedAtUtc != null, ct), "/admin/roles"),
            new("Setup.Numbering", await db.DocumentSequences.AnyAsync(s => s.UpdatedAtUtc != null, ct), "/admin/numbering"),
        };

        return new LabDashboardDto(t.Name, t.Status.ToString(), sub?.Name ?? "-", sub?.EndDate, activeUsers, sub?.MaxUsers ?? 0,
            branches, sub?.MaxBranches ?? 0, departments, invitations, roles, recent, steps);
    }
}
