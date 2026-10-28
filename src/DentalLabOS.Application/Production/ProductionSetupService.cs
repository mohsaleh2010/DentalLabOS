using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Production;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Production;

/// <summary>Reason codes (ADM-18), technician profiles (ADM-07) and the branch picker (AUTH-08).</summary>
public sealed class ProductionSetupService(
    IApplicationDbContext db, IPermissionService permissions, ICurrentUser currentUser, IBranchSelection branchSelection)
{
    private static readonly WorkOrderStatus[] Closed = [WorkOrderStatus.Delivered, WorkOrderStatus.Cancelled];

    // ---- Reason codes ----

    public async Task<IReadOnlyList<ReasonCodeDto>> ListReasonsAsync(string? scope = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.ReasonsManage, ct);
        var query = db.ReasonCodes.AsNoTracking();
        if (MasterData.TryParseEnum<ReasonScope>(scope, out var sc))
        {
            query = query.Where(r => r.Scope == sc);
        }

        return await query.OrderBy(r => r.Scope).ThenBy(r => r.Name)
            .Select(r => new ReasonCodeDto(r.Id, r.Scope.ToString(), r.Category.ToString(), r.Name, r.IsActive,
                db.Holds.Count(h => h.ReasonCodeId == r.Id), Convert.ToBase64String(r.RowVersion)))
            .ToListAsync(ct);
    }

    /// <summary>Active reasons of one scope for the hold and cancel dialogs.</summary>
    public async Task<IReadOnlyList<ReasonCodeDto>> ReasonOptionsAsync(ReasonScope scope, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.WorkOrders.Read, ct);
        return await db.ReasonCodes.AsNoTracking().Where(r => r.Scope == scope && r.IsActive).OrderBy(r => r.Name)
            .Select(r => new ReasonCodeDto(r.Id, r.Scope.ToString(), r.Category.ToString(), r.Name, r.IsActive, 0, string.Empty))
            .ToListAsync(ct);
    }

    public async Task SaveReasonAsync(Guid? id, ReasonCodeUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.ReasonsManage, ct);
        var name = request.Name?.Trim();
        var v = new Validator().Required(nameof(request.Name), name).MaxLength(nameof(request.Name), name, 150)
            .When(!MasterData.TryParseEnum<ReasonScope>(request.Scope, out var scope), nameof(request.Scope), "Validation.Required")
            .When(!MasterData.TryParseEnum<ReasonCategory>(request.Category, out var category), nameof(request.Category), "Validation.Required");
        v.When(name is not null && await db.ReasonCodes.AnyAsync(r => r.Scope == scope && r.Name == name && r.Id != id, ct), nameof(request.Name), "Validation.NameTaken");
        v.ThrowIfInvalid();

        var reason = id is { } existing
            ? await db.ReasonCodes.FirstOrDefaultAsync(r => r.Id == existing, ct) ?? throw new NotFoundException(nameof(ReasonCode))
            : db.ReasonCodes.Add(new ReasonCode()).Entity;
        if (id is not null)
        {
            db.SetOriginalRowVersion(reason, request.RowVersion);
        }

        reason.Scope = scope;
        reason.Category = category;
        reason.Name = name!;
        reason.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
    }

    // ---- Technicians ----

    public async Task<TechnicianProfileDto> GetTechnicianAsync(Guid userProfileId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.TechniciansManage, ct);
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userProfileId, ct) ?? throw new NotFoundException(nameof(UserProfile));
        var tech = await db.TechnicianProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.UserProfileId == userProfileId, ct);
        var canSeeRate = await permissions.HasAsync(Permissions.Admin.CostingManage, ct);
        var deptName = tech is null ? null : await db.Departments.Where(d => d.Id == tech.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        var open = await OpenStages().CountAsync(s => s.AssignedUserId == profile.UserId, ct);
        return new TechnicianProfileDto(profile.Id, profile.FullName, tech is not null, tech?.DepartmentId, deptName,
            canSeeRate ? tech?.LaborRatePerHour : null, tech?.Skills, tech?.DifficultyCapacityPerDay ?? 0, open,
            tech is null ? null : RowVersions.Encode(tech.RowVersion));
    }

    public async Task SaveTechnicianAsync(Guid userProfileId, TechnicianProfileRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.TechniciansManage, ct);
        if (!await db.UserProfiles.AnyAsync(p => p.Id == userProfileId, ct))
        {
            throw new NotFoundException(nameof(UserProfile));
        }

        var v = new Validator().MaxLength(nameof(request.Skills), request.Skills, 1000)
            .Range(nameof(request.DifficultyCapacityPerDay), request.DifficultyCapacityPerDay, 0, 1000)
            .Range(nameof(request.LaborRatePerHour), request.LaborRatePerHour ?? 0, 0, 1_000_000m);
        v.When(!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, ct), nameof(request.DepartmentId), "Validation.Required");
        v.ThrowIfInvalid();

        var tech = await db.TechnicianProfiles.FirstOrDefaultAsync(t => t.UserProfileId == userProfileId, ct);
        if (tech is null)
        {
            tech = db.TechnicianProfiles.Add(new TechnicianProfile { UserProfileId = userProfileId }).Entity;
        }
        else
        {
            db.SetOriginalRowVersion(tech, request.RowVersion);
        }

        tech.DepartmentId = request.DepartmentId;
        tech.Skills = MasterData.Clean(request.Skills);
        tech.DifficultyCapacityPerDay = request.DifficultyCapacityPerDay;

        // The rate feeds labour costing; without the costing permission it is neither shown nor changed.
        if (request.LaborRatePerHour is { } rate && await permissions.HasAsync(Permissions.Admin.CostingManage, ct))
        {
            tech.LaborRatePerHour = rate;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>People work can be assigned to: technicians (optionally of one department), or every active user if no profiles exist yet.</summary>
    public async Task<IReadOnlyList<TechnicianOptionDto>> TechnicianOptionsAsync(Guid? departmentId = null, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Production.Read, ct);
        var techs = await (from t in db.TechnicianProfiles.AsNoTracking()
                           join p in db.UserProfiles.AsNoTracking() on t.UserProfileId equals p.Id
                           where p.IsActive && (departmentId == null || t.DepartmentId == departmentId)
                           select new { p.UserId, p.FullName, t.DepartmentId }).ToListAsync(ct);
        if (techs.Count == 0 && !await db.TechnicianProfiles.AnyAsync(ct))
        {
            techs = await db.UserProfiles.AsNoTracking().Where(p => p.IsActive)
                .Select(p => new { p.UserId, p.FullName, DepartmentId = Guid.Empty }).ToListAsync(ct);
        }

        var userIds = techs.Select(t => t.UserId).ToList();
        var load = await OpenStages().Where(s => s.AssignedUserId != null && userIds.Contains(s.AssignedUserId.Value))
            .GroupBy(s => s.AssignedUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var deptNames = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return techs.OrderBy(t => t.FullName)
            .Select(t => new TechnicianOptionDto(t.UserId, t.FullName, t.DepartmentId == Guid.Empty ? null : t.DepartmentId,
                deptNames.GetValueOrDefault(t.DepartmentId), load.GetValueOrDefault(t.UserId)))
            .ToList();
    }

    // ---- Branch context ----

    /// <summary>Branches the user may work in: their branch scope, or every active branch when they have none.</summary>
    public async Task<IReadOnlyList<BranchOptionDto>> MyBranchesAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("context");
        var scoped = await (from s in db.UserBranchScopes.AsNoTracking()
                            join p in db.UserProfiles.AsNoTracking() on s.UserProfileId equals p.Id
                            where p.UserId == userId
                            select s.BranchId).ToListAsync(ct);
        return await db.Branches.AsNoTracking().Where(b => b.IsActive && (scoped.Count == 0 || scoped.Contains(b.Id)))
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
            .Select(b => new BranchOptionDto(b.Id, b.Code, b.Name, b.IsMain)).ToListAsync(ct);
    }

    /// <summary>The branch new documents go to: the requested one if the user may use it, else their first branch.</summary>
    public async Task<Guid> ResolveBranchAsync(Guid? requested, CancellationToken ct = default)
    {
        var branches = await MyBranchesAsync(ct);
        var wanted = requested ?? branchSelection.RequestedBranchId;
        if (wanted is { } w && branches.Any(b => b.Id == w))
        {
            return w;
        }

        if (requested is not null)
        {
            throw ValidationException.For("BranchId", "Validation.BranchScope");
        }

        return branches.FirstOrDefault()?.Id ?? throw ValidationException.For("BranchId", "Validation.Required");
    }

    private IQueryable<WorkOrderStage> OpenStages() =>
        db.WorkOrderStages.Where(s => (s.Status == StageStatus.Pending || s.Status == StageStatus.InProgress || s.Status == StageStatus.Paused)
                                      && !Closed.Contains(s.WorkOrder.Status));
}
