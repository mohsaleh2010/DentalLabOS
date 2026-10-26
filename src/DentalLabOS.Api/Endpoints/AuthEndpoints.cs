using DentalLabOS.Api.Infrastructure;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Contracts.Auth;
using DentalLabOS.Contracts;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/token", IssueTokenAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.Auth)
            .WithSummary("Exchange email and password for a short-lived access token");

        api.MapGet("/me", GetMeAsync)
            .WithTags("Auth")
            .WithSummary("The signed-in user, their lab and effective permissions");

        return api;
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> IssueTokenAsync(
        TokenRequest request,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IUserClaimsPrincipalFactory<ApplicationUser> principals,
        TokenIssuer tokens,
        IAuditWriter audit,
        AppDbContext db,
        TimeProvider time)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email.Trim());
        var result = user is null || string.IsNullOrEmpty(request.Password)
            ? SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (user is not null)
            {
                audit.Add(AuditActions.LoginFailed, "User", user.Id, result.IsLockedOut ? "LockedOut" : result.IsNotAllowed ? "NotAllowed" : "BadPassword", tenantId: user.TenantId);
                await db.SaveChangesAsync();
            }

            // Same answer for unknown e-mail and wrong password (no account enumeration).
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials",
                extensions: new Dictionary<string, object?> { ["code"] = result.IsLockedOut ? "locked_out" : ErrorCodes.Unauthorized });
        }

        user!.LastLoginAtUtc = time.GetUtcNow().UtcDateTime;
        await users.UpdateAsync(user);
        audit.Add(AuditActions.Login, "User", user.Id, "Api", tenantId: user.TenantId);
        await db.SaveChangesAsync();

        var (token, expires) = tokens.Issue(await principals.CreateAsync(user));
        return TypedResults.Ok(new TokenResponse(token, expires));
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        ICurrentUser current, IPermissionService permissions, UserManager<ApplicationUser> users, AppDbContext db, CancellationToken ct)
    {
        if (current.UserId is not { } id || await users.FindByIdAsync(id.ToString()) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        var tenantName = user.TenantId is { } tid
            ? await db.Tenants.Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;
        var granted = await permissions.GetGrantedAsync(ct);
        return TypedResults.Ok(new MeResponse(
            user.Id, user.Email!, user.FullName, user.TenantId, tenantName, user.PlatformRole != Domain.Common.PlatformRole.None,
            user.PreferredCulture, user.Theme, granted.Order(StringComparer.Ordinal).ToArray()));
    }
}
