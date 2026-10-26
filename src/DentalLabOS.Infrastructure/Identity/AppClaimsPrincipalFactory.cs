using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DentalLabOS.Infrastructure.Identity;

/// <summary>Adds tenant and display claims. Permissions are NOT put in the cookie: they are resolved per request.</summary>
public sealed class AppClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.TenantId is { } tid)
        {
            identity.AddClaim(new Claim(AppClaimTypes.TenantId, tid.ToString()));
        }

        identity.AddClaim(new Claim(AppClaimTypes.PlatformRole, user.PlatformRole.ToString()));
        identity.AddClaim(new Claim(AppClaimTypes.FullName, user.FullName));
        identity.AddClaim(new Claim(AppClaimTypes.Culture, user.PreferredCulture));
        identity.AddClaim(new Claim(AppClaimTypes.Theme, user.Theme));
        return identity;
    }
}

/// <summary>Blocks sign-in for deactivated users and for users of suspended/closed labs.</summary>
public sealed class AppSignInManager(
    UserManager<ApplicationUser> userManager,
    Microsoft.AspNetCore.Http.IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    Microsoft.Extensions.Logging.ILogger<SignInManager<ApplicationUser>> logger,
    Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    Persistence.AppDbContext db)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!user.IsActive || !await base.CanSignInAsync(user))
        {
            return false;
        }

        if (user.TenantId is { } tid)
        {
            var tenant = await db.Tenants.FindAsync(tid);
            return tenant is { CanSignIn: true };
        }

        return user.PlatformRole != Domain.Common.PlatformRole.None;
    }
}
