using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Security;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Infrastructure.Security;

public enum AccessState { Anonymous, Active, UserDisabled, TenantSuspended }

/// <summary>
/// Resolves the current user's permissions from the database once per request (so revocations apply immediately),
/// and reports whether the account/lab is still allowed in.
/// </summary>
public sealed class PermissionService(AppDbContext db, ICurrentUser currentUser) : IPermissionService
{
    private (AccessState State, IReadOnlySet<string> Granted)? _cache;

    public async Task<AccessState> GetAccessStateAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).State;

    public async Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).Granted;

    public async Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default) =>
        (await GetGrantedAsync(cancellationToken)).Contains(permission);

    public async Task DemandAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!await HasAsync(permission, cancellationToken))
        {
            throw new ForbiddenException(permission);
        }
    }

    public void Invalidate() => _cache = null;

    private async Task<(AccessState State, IReadOnlySet<string> Granted)> LoadAsync(CancellationToken ct)
    {
        if (_cache is { } cached)
        {
            return cached;
        }

        var empty = (IReadOnlySet<string>)new HashSet<string>();
        if (currentUser.UserId is not { } userId)
        {
            return (_cache = (AccessState.Anonymous, empty)).Value;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.TenantId, u.PlatformRole })
            .FirstOrDefaultAsync(ct);

        if (user is null || !user.IsActive)
        {
            return (_cache = (AccessState.UserDisabled, empty)).Value;
        }

        // Platform staff: permissions come from their platform role (they hold no tenant data permissions).
        if (user.TenantId is null)
        {
            var platform = Permissions.ForPlatformRole(user.PlatformRole).ToHashSet(StringComparer.Ordinal);
            return (_cache = (AccessState.Active, platform)).Value;
        }

        // A token minted for one tenant must never be used against another.
        if (currentUser.TenantId != user.TenantId)
        {
            return (_cache = (AccessState.UserDisabled, empty)).Value;
        }

        var tenantOk = await db.Tenants.AsNoTracking().Where(t => t.Id == user.TenantId)
            .Select(t => t.Status == Domain.Common.TenantStatus.Active || t.Status == Domain.Common.TenantStatus.Trial)
            .FirstOrDefaultAsync(ct);
        if (!tenantOk)
        {
            return (_cache = (AccessState.TenantSuspended, empty)).Value;
        }

        var keys = await db.UserProfiles
            .Where(p => p.UserId == userId && p.IsActive)
            .SelectMany(p => p.Roles)
            .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        return (_cache = (AccessState.Active, keys.ToHashSet(StringComparer.Ordinal))).Value;
    }
}
