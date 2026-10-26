using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

public sealed record InvitationPreview(string LabName, string Email, string FullName, string RoleName);

/// <summary>
/// AUTH-05: accepting an invitation is anonymous, so the invitation is located by its token hash across tenants
/// (an allow-listed filter bypass). All writes then happen inside that invitation's tenant scope.
/// </summary>
public sealed class InvitationService(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    IIdentityService identity,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<InvitationPreview?> PreviewAsync(string token, CancellationToken ct = default)
    {
        var invitation = await FindUsableAsync(token, ct);
        if (invitation is null)
        {
            return null;
        }

        var lab = await db.Tenants.AsNoTracking().Where(t => t.Id == invitation.TenantId).Select(t => t.Name).FirstAsync(ct);
        var role = await db.Roles.IgnoreQueryFilters().AsNoTracking().Where(r => r.Id == invitation.RoleId).Select(r => r.Name).FirstOrDefaultAsync(ct);
        return new InvitationPreview(lab, invitation.Email, invitation.FullName, role ?? string.Empty);
    }

    /// <summary>Creates the login, the lab profile and the role link. Returns the new user id.</summary>
    public async Task<Guid> AcceptAsync(string token, string password, CancellationToken ct = default)
    {
        var invitation = await FindUsableAsync(token, ct) ?? throw ValidationException.For("Token", "Validation.InvitationInvalid");
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == invitation.TenantId, ct);
        if (!tenant.CanSignIn)
        {
            throw ValidationException.For("Token", "Errors.Tenant.Suspended");
        }

        if (await identity.EmailExistsAsync(invitation.Email, ct))
        {
            throw ValidationException.For("Token", "Validation.EmailTaken");
        }

        using var scope = tenantContext.BeginScope(invitation.TenantId);
        return await db.InTransactionAsync(async innerCt =>
        {
            var userId = await identity.CreateUserAsync(invitation.Email, invitation.FullName, password, invitation.TenantId,
                Domain.Common.PlatformRole.None, tenant.DefaultCulture, innerCt);

            var tracked = await db.UserInvitations.FirstAsync(i => i.Id == invitation.Id, innerCt);
            tracked.AcceptedAtUtc = clock.UtcNow;

            var profile = new UserProfile { UserId = userId, FullName = invitation.FullName };
            db.UserProfiles.Add(profile);
            db.UserRoles.Add(new UserRole { UserProfileId = profile.Id, RoleId = invitation.RoleId });
            audit.Add(AuditActions.InviteAccepted, nameof(UserInvitation), invitation.Id, null, new { invitation.Email }, invitation.TenantId);
            await db.SaveChangesAsync(innerCt);
            return userId;
        }, ct);
    }

    private async Task<UserInvitation?> FindUsableAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return null;
        }

        var hash = SecureToken.Hash(token.Trim());
        var now = clock.UtcNow;
        return await db.UserInvitations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct);
    }
}
