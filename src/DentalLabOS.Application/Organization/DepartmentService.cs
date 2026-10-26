using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Organization;

/// <summary>ADM-04 departments (production departments drive queues in phase 3).</summary>
public sealed class DepartmentService(IApplicationDbContext db, IPermissionService permissions)
{
    public async Task<PagedResult<DepartmentDto>> ListAsync(PageQuery page, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.DepartmentsManage, ct);

        var query = db.Departments.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(d => d.Name.Contains(s) || d.Code.Contains(s));
        }

        return await query
            .OrderBy(d => d.Code)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.BranchId, d.Branch != null ? d.Branch.Name : null, d.IsProduction, d.IsActive,
                Convert.ToBase64String(d.RowVersion)))
            .ToPagedAsync(page, ct);
    }

    public async Task<DepartmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.DepartmentsManage, ct);
        return await db.Departments.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.BranchId, d.Branch != null ? d.Branch.Name : null, d.IsProduction, d.IsActive,
                Convert.ToBase64String(d.RowVersion)))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Department));
    }

    public async Task<Guid> CreateAsync(DepartmentUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.DepartmentsManage, ct);
        await ValidateAsync(request, null, ct);
        var department = new Department
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            BranchId = request.BranchId,
            IsProduction = request.IsProduction,
            IsActive = true,
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return department.Id;
    }

    public async Task UpdateAsync(Guid id, DepartmentUpsertRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.DepartmentsManage, ct);
        var department = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Department));
        await ValidateAsync(request, id, ct);
        db.SetOriginalRowVersion(department, request.RowVersion);
        department.Code = request.Code.Trim().ToUpperInvariant();
        department.Name = request.Name.Trim();
        department.BranchId = request.BranchId;
        department.IsProduction = request.IsProduction;
        department.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateAsync(DepartmentUpsertRequest r, Guid? id, CancellationToken ct)
    {
        var v = new Validator()
            .Required(nameof(r.Code), r.Code).Pattern(nameof(r.Code), r.Code, BranchService.CodePattern, "Validation.Code")
            .Required(nameof(r.Name), r.Name).MaxLength(nameof(r.Name), r.Name, 200);

        var code = r.Code?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(code) && await db.Departments.AnyAsync(d => d.Code == code && d.Id != id, ct))
        {
            v.Add(nameof(r.Code), "Validation.CodeTaken");
        }

        // The branch must belong to the same tenant: the filtered set guarantees that.
        if (r.BranchId is { } branchId && !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            v.Add(nameof(r.BranchId), "Validation.Required");
        }

        v.ThrowIfInvalid();
    }
}
