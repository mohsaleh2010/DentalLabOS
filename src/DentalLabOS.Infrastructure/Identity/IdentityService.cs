using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Domain.Common;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Infrastructure.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> users, AppDbContext db, IClock clock) : IIdentityService
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await users.FindByEmailAsync(email.Trim()) is not null;

    public async Task<Guid> CreateUserAsync(string email, string fullName, string password, Guid? tenantId, PlatformRole platformRole, string culture,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            FullName = fullName.Trim(),
            TenantId = tenantId,
            PlatformRole = platformRole,
            PreferredCulture = culture,
            CreatedAtUtc = clock.UtcNow,
            LockoutEnabled = true,
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["Password"] = result.Errors.Select(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? $"Identity.{e.Code}" : "Validation.EmailTaken").Distinct().ToArray(),
            });
        }

        return user.Id;
    }

    public async Task<IReadOnlyDictionary<Guid, IdentityUserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, IdentityUserSummary>();
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new IdentityUserSummary(u.Id, u.Email ?? string.Empty, u.FullName, u.LastLoginAtUtc, u.IsActive))
            .ToDictionaryAsync(u => u.UserId, cancellationToken);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException(nameof(ApplicationUser));
        user.IsActive = isActive;
        await users.UpdateAsync(user);

        // Rotating the stamp invalidates existing cookies at the next validation interval.
        await users.UpdateSecurityStampAsync(user);
    }

    public Task<bool> AnyPlatformOwnerAsync(CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.PlatformRole == PlatformRole.Owner, cancellationToken);
}
