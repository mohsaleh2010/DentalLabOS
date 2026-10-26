using DentalLabOS.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace DentalLabOS.Infrastructure.Identity;

/// <summary>
/// The login account. <see cref="TenantId"/> is null for platform staff. Lab-specific data lives in
/// <see cref="Domain.Organization.UserProfile"/>; display preferences live here because platform staff need them too.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public Guid? TenantId { get; set; }
    public string FullName { get; set; } = default!;
    public PlatformRole PlatformRole { get; set; } = PlatformRole.None;
    public bool IsActive { get; set; } = true;
    public string PreferredCulture { get; set; } = "ar-EG";
    public string Theme { get; set; } = "system";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}

public static class AppClaimTypes
{
    public const string TenantId = "dlos:tid";
    public const string PlatformRole = "dlos:prole";
    public const string FullName = "dlos:name";
    public const string Culture = "dlos:culture";
    public const string Theme = "dlos:theme";
}
