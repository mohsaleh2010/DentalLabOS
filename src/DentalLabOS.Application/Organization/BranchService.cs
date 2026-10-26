using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Organization;

/// <summary>ADM-03 branches. Tenant scoping comes from global query filters; limits from the current plan.</summary>
public sealed class BranchService(IApplicationDbContext db, IPermissionService permissions, ITenantContext tenant)
{
    public const string CodePattern = "^[A-Za-z0-9_-]{1,20}$";

    public async Task<PagedResult<BranchDto>> ListAsync(PageQuery page, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.BranchesManage, ct);

        var query = db.Branches.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(b => b.Name.Contains(s) || b.Code.Contains(s));
        }

        return await query
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Code)
            .Select(b => new BranchDto(b.Id, b.Code, b.Name, b.Address, b.Phone, b.IsMain, b.IsActive, Convert.ToBase64String(b.RowVersion)))
            .ToPagedAsync(page, ct);
    }

    /// <summary>Active branches for pickers. Branch names are not sensitive, so any lab user may list them.</summary>
    public async Task<IReadOnlyList<BranchDto>> ListOptionsAsync(CancellationToken ct = default)
    {
        if (tenant.TenantId is null)
        {
            throw new ForbiddenException("tenant");
        }

        return await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Code, b.Name, b.Address, b.Phone, b.IsMain, b.IsActive, Convert.ToBase64String(b.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task<BranchDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.BranchesManage, ct);
        var b = await db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Branch));
        return Map(b);
    }

    public async Task<BranchDto> CreateAsync(BranchUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.BranchesManage, ct);
        await ValidateAsync(request, null, ct);

        var limit = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenant.TenantId && s.IsCurrent)
            .Select(s => (int?)s.Plan.MaxBranches)
            .FirstOrDefaultAsync(ct);
        var count = await db.Branches.CountAsync(b => b.IsActive, ct);
        if (limit is > 0 && count >= limit)
        {
            throw ValidationException.For(nameof(request.Code), "Validation.PlanLimitBranches");
        }

        var branch = new Branch
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = true,
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);
        return Map(branch);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, BranchUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.BranchesManage, ct);
        var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Branch));
        await ValidateAsync(request, id, ct);

        if (branch.IsMain && !request.IsActive)
        {
            throw ValidationException.For(nameof(request.IsActive), "Validation.MainBranchActive");
        }

        db.SetOriginalRowVersion(branch, request.RowVersion);
        branch.Code = request.Code.Trim().ToUpperInvariant();
        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        branch.Phone = request.Phone?.Trim();
        branch.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Map(branch);
    }

    private async Task ValidateAsync(BranchUpsertRequest r, Guid? id, CancellationToken ct)
    {
        var v = new Validator()
            .Required(nameof(r.Code), r.Code).Pattern(nameof(r.Code), r.Code, CodePattern, "Validation.Code")
            .Required(nameof(r.Name), r.Name).MaxLength(nameof(r.Name), r.Name, 200)
            .MaxLength(nameof(r.Address), r.Address, 500).MaxLength(nameof(r.Phone), r.Phone, 50);

        var code = r.Code?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(code) && await db.Branches.AnyAsync(b => b.Code == code && b.Id != id, ct))
        {
            v.Add(nameof(r.Code), "Validation.CodeTaken");
        }

        v.ThrowIfInvalid();
    }

    private static BranchDto Map(Branch b) =>
        new(b.Id, b.Code, b.Name, b.Address, b.Phone, b.IsMain, b.IsActive, RowVersions.Encode(b.RowVersion));
}
