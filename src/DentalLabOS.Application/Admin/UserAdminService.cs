using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

/// <summary>ADM-05/06: lab users, invitations and role assignment. Plan user limits are enforced here.</summary>
public sealed class UserAdminService(
    IApplicationDbContext db,
    IPermissionService permissions,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IIdentityService identity,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<PagedResult<UserSummaryDto>> ListAsync(PageQuery page, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersRead, ct);

        var query = db.UserProfiles.AsNoTracking();
        if (page.NormalizedSearch() is { } s)
        {
            query = query.Where(u => u.FullName.Contains(s) || (u.JobTitle != null && u.JobTitle.Contains(s)));
        }

        var paged = await query.OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.UserId,
                u.FullName,
                u.JobTitle,
                u.IsActive,
                Roles = u.Roles.Select(r => r.Role.Name).ToList(),
            })
            .ToPagedAsync(page, ct);

        var summaries = await identity.GetSummariesAsync(paged.Items.Select(i => i.UserId), ct);
        var items = paged.Items.Select(u =>
        {
            summaries.TryGetValue(u.UserId, out var idu);
            return new UserSummaryDto(u.Id, u.UserId, u.FullName, idu?.Email ?? string.Empty, u.JobTitle, u.Roles, u.IsActive, idu?.LastLoginAtUtc);
        }).ToList();

        return new PagedResult<UserSummaryDto>(items, paged.Page, paged.PageSize, paged.TotalCount);
    }

    public async Task<IReadOnlyList<InvitationDto>> ListInvitationsAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersRead, ct);
        return await db.UserInvitations.AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(50)
            .Select(i => new InvitationDto(i.Id, i.Email, i.FullName,
                db.Roles.Where(r => r.Id == i.RoleId).Select(r => r.Name).FirstOrDefault() ?? string.Empty,
                i.ExpiresAtUtc, i.AcceptedAtUtc != null, i.RevokedAtUtc != null))
            .ToListAsync(ct);
    }

    public async Task<InviteUserResult> InviteAsync(InviteUserRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersManage, ct);

        var email = request.Email?.Trim() ?? string.Empty;
        var v = new Validator()
            .Required(nameof(request.Email), email).Email(nameof(request.Email), email)
            .Required(nameof(request.FullName), request.FullName).MaxLength(nameof(request.FullName), request.FullName, 200);

        if (!await db.Roles.AnyAsync(r => r.Id == request.RoleId, ct))
        {
            v.Add(nameof(request.RoleId), "Validation.Required");
        }

        if (!string.IsNullOrEmpty(email) && await identity.EmailExistsAsync(email, ct))
        {
            v.Add(nameof(request.Email), "Validation.EmailTaken");
        }

        var now = clock.UtcNow;
        if (!string.IsNullOrEmpty(email) && await db.UserInvitations.AnyAsync(i => i.Email == email && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct))
        {
            v.Add(nameof(request.Email), "Validation.InvitationPending");
        }

        var maxUsers = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenant.TenantId && s.IsCurrent)
            .Select(s => (int?)s.Plan.MaxUsers).FirstOrDefaultAsync(ct);
        var used = await db.UserProfiles.CountAsync(u => u.IsActive, ct)
                   + await db.UserInvitations.CountAsync(i => i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct);
        v.When(maxUsers is > 0 && used >= maxUsers, nameof(request.Email), "Validation.PlanLimitUsers");
        v.ThrowIfInvalid();

        var token = SecureToken.Create();
        var invitation = new UserInvitation
        {
            Email = email,
            FullName = request.FullName.Trim(),
            RoleId = request.RoleId,
            TokenHash = SecureToken.Hash(token),
            ExpiresAtUtc = now.AddDays(7),
        };
        db.UserInvitations.Add(invitation);
        audit.Add(AuditActions.Invite, nameof(UserInvitation), invitation.Id, null, new { invitation.Email, invitation.RoleId });
        await db.SaveChangesAsync(ct);
        return new InviteUserResult(invitation.Id, token, invitation.ExpiresAtUtc);
    }

    public async Task RevokeInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersManage, ct);
        var invitation = await db.UserInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct) ?? throw new NotFoundException(nameof(UserInvitation));
        if (invitation.AcceptedAtUtc is null && invitation.RevokedAtUtc is null)
        {
            invitation.RevokedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task SetActiveAsync(Guid profileId, bool isActive, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersManage, ct);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct) ?? throw new NotFoundException(nameof(UserProfile));

        if (!isActive)
        {
            if (profile.UserId == currentUser.UserId)
            {
                throw ValidationException.For("User", "Validation.CannotDeactivateSelf");
            }

            if (await IsLastActiveOwnerAsync(profile.Id, ct))
            {
                throw ValidationException.For("User", "Validation.LastOwner");
            }
        }

        profile.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        await identity.SetActiveAsync(profile.UserId, isActive, ct);
        permissions.Invalidate();
    }

    public async Task AssignRolesAsync(Guid profileId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersManage, ct);
        var profile = await db.UserProfiles.Include(p => p.Roles).FirstOrDefaultAsync(p => p.Id == profileId, ct)
                      ?? throw new NotFoundException(nameof(UserProfile));

        var valid = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => new { r.Id, r.SystemKey }).ToListAsync(ct);
        if (valid.Count == 0)
        {
            throw ValidationException.For("Roles", "Validation.Required");
        }

        var ownerRoleId = await db.Roles.Where(r => r.SystemKey == SystemRoles.Owner).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var losesOwner = profile.Roles.Any(r => r.RoleId == ownerRoleId) && valid.All(r => r.Id != ownerRoleId);
        if (losesOwner && await IsLastActiveOwnerAsync(profile.Id, ct))
        {
            throw ValidationException.For("Roles", "Validation.LastOwner");
        }

        var before = profile.Roles.Select(r => r.RoleId).ToList();
        db.UserRoles.RemoveRange(profile.Roles.Where(r => valid.All(v => v.Id != r.RoleId)));
        foreach (var r in valid.Where(v => profile.Roles.All(x => x.RoleId != v.Id)))
        {
            db.UserRoles.Add(new UserRole { UserProfileId = profile.Id, RoleId = r.Id });
        }

        audit.Add(AuditActions.RoleAssignment, nameof(UserProfile), profile.Id, null, new { before, after = valid.Select(v => v.Id) });
        await db.SaveChangesAsync(ct);
        permissions.Invalidate();
    }

    public async Task<(UserSummaryDto User, IReadOnlyList<Guid> RoleIds)> GetAsync(Guid profileId, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.UsersRead, ct);
        var p = await db.UserProfiles.AsNoTracking().Include(x => x.Roles).ThenInclude(r => r.Role)
                    .FirstOrDefaultAsync(x => x.Id == profileId, ct) ?? throw new NotFoundException(nameof(UserProfile));
        var summaries = await identity.GetSummariesAsync([p.UserId], ct);
        summaries.TryGetValue(p.UserId, out var idu);
        return (new UserSummaryDto(p.Id, p.UserId, p.FullName, idu?.Email ?? string.Empty, p.JobTitle,
            p.Roles.Select(r => r.Role.Name).ToList(), p.IsActive, idu?.LastLoginAtUtc), p.Roles.Select(r => r.RoleId).ToList());
    }

    private async Task<bool> IsLastActiveOwnerAsync(Guid profileId, CancellationToken ct)
    {
        var owners = await db.UserRoles
            .Where(r => r.Role.SystemKey == SystemRoles.Owner)
            .Join(db.UserProfiles.Where(p => p.IsActive), r => r.UserProfileId, p => p.Id, (r, p) => p.Id)
            .Distinct()
            .ToListAsync(ct);
        return owners.Count == 1 && owners[0] == profileId;
    }
}
