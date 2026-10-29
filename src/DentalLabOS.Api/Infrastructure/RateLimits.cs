using System.Threading.RateLimiting;
using DentalLabOS.Contracts;

namespace DentalLabOS.Api.Infrastructure;

public static class RateLimits
{
    public const string Auth = "auth";

    /// <summary>
    /// Per-IP limit on token requests and a per-user (or per-IP) limit on everything else.
    /// <c>RateLimits:AuthPerMinute</c> and <c>RateLimits:UserBurst</c> override the defaults (10 and 120).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration) => services.AddRateLimiter(o =>
    {
        var authPerMinute = configuration.GetValue("RateLimits:AuthPerMinute", 10);
        var userBurst = configuration.GetValue("RateLimits:UserBurst", 120);
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetTokenBucketLimiter(
            ctx.User.Identity?.IsAuthenticated == true ? "u:" + ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : "ip:" + ctx.Connection.RemoteIpAddress,
            _ => new TokenBucketRateLimiterOptions { TokenLimit = userBurst, TokensPerPeriod = Math.Max(1, userBurst / 2), ReplenishmentPeriod = TimeSpan.FromSeconds(30), QueueLimit = 0 }));
        o.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.ContentType = "application/problem+json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(
                new { status = 429, title = "Too many requests", code = ErrorCodes.RateLimited }, ct);
        };
    });
}
