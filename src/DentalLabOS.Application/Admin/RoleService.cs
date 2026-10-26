using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

/// <summary>ADM-08/09 roles and the permission matrix. The owner role is locked to the full catalog.</summary>
public sealed class RoleService(IApplicationDbContext db, IPermissionService permissions, IAuditWriter audit)
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.RolesManage, ct);

        return await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.SystemKey == SystemRoles.Owner).ThenByDescending(r => r.IsSystemRole).ThenBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.SystemKey, r.Description, r.IsSystemRole,
                db.RolePermissions.Count(p => p.RoleId == r.Id),
                db.UserRoles.Count(u => u.RoleId == r.Id)))
            .ToListAsync(ct);
    }

    /// <summary>Roles available for assignment (used by user invite forms).</summary>
    public async Task<IReadOnlyList<RoleDto>> ListAssignableAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersManage, ct);
        return await db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.SystemKey, r.Description, r.IsSystemRole, 0, 0))
            .ToListAsync(ct);
    }

    public async Task<RolePermissionsDto> GetPermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.RolesManage, ct);

        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct) ?? throw new NotFoundException(nameof(AppRole));
        var granted = await db.RolePermissions.AsNoTracking().Where(p => p.RoleId == roleId).Select(p => p.Permission.Key).ToListAsync(ct);
        var grantedSet = granted.ToHashSet(StringComparer.Ordinal);

        var items = Permissions.TenantKeys
            .Select(k => new PermissionItemDto(k, Permissions.ModuleOf(k), grantedSet.Contains(k)))
            .ToList();

        return new RolePermissionsDto(role.Id, role.Name, role.SystemKey == SystemRoles.Owner, items, RowVersions.Encode(role.RowVersion));
    }

    public async Task UpdatePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.RolesManage, ct);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct) ?? throw new NotFoundException(nameof(AppRole));
        if (role.SystemKey == SystemRoles.Owner)
        {
            throw ValidationException.For("Permissions", "Validation.OwnerRoleLocked");
        }

        var requested = request.PermissionKeys.Where(Permissions.TenantKeys.Contains).ToHashSet(StringComparer.Ordinal);
        var current = await db.RolePermissions.Where(p => p.RoleId == roleId).Include(p => p.Permission).ToListAsync(ct);
        var currentKeys = current.Select(p => p.Permission.Key).ToHashSet(StringComparer.Ordinal);

        var removed = current.Where(p => !requested.Contains(p.Permission.Key)).ToList();
        var addedKeys = requested.Where(k => !currentKeys.Contains(k)).ToList();
        if (removed.Count == 0 && addedKeys.Count == 0)
        {
            return;
        }

        // Touch the role so concurrent matrix edits conflict on its RowVersion.
        db.SetOriginalRowVersion(role, request.RowVersion);
        role.UpdatedAtUtc = DateTime.UtcNow;

        db.RolePermissions.RemoveRange(removed);
        var ids = await db.Permissions.AsNoTracking().Where(p => addedKeys.Contains(p.Key)).ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        foreach (var key in addedKeys)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = ids[key] });
        }

        audit.Add(AuditActions.PermissionChange, nameof(AppRole), roleId, null,
            new { role = role.Name, added = addedKeys, removed = removed.Select(r => r.Permission.Key).ToList() });
        await db.SaveChangesAsync(ct);
        permissions.Invalidate();
    }

    public async Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.RolesManage, ct);

        var v = new Validator().Required(nameof(request.Name), request.Name).MaxLength(nameof(request.Name), request.Name, 100);
        var name = request.Name?.Trim();
        if (!string.IsNullOrEmpty(name) && await db.Roles.AnyAsync(r => r.Name == name, ct))
        {
            v.Add(nameof(request.Name), "Validation.NameTaken");
        }

        v.ThrowIfInvalid();

        var role = new AppRole { Name = name!, Description = request.Description?.Trim() };
        db.Roles.Add(role);
        if (request.CopyFromRoleId is { } source)
        {
            var sourcePermissions = await db.RolePermissions.AsNoTracking().Where(p => p.RoleId == source).Select(p => p.PermissionId).ToListAsync(ct);
            foreach (var pid in sourcePermissions)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
            }
        }

        await db.SaveChangesAsync(ct);
        return role.Id;
    }
}
