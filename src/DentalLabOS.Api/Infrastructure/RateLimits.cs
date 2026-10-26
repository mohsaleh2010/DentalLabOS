using System.Threading.RateLimiting;
using DentalLabOS.Contracts;

namespace DentalLabOS.Api.Infrastructure;

public static class RateLimits
{
    public const string Auth = "auth";

    /// <summary>Per-IP limit on token requests and a per-user (or per-IP) limit on everything else.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) => services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetTokenBucketLimiter(
            ctx.User.Identity?.IsAuthenticated == true ? "u:" + ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : "ip:" + ctx.Connection.RemoteIpAddress,
            _ => new TokenBucketRateLimiterOptions { TokenLimit = 120, TokensPerPeriod = 60, ReplenishmentPeriod = TimeSpan.FromSeconds(30), QueueLimit = 0 }));
        o.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.ContentType = "application/problem+json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(
                new { status = 429, title = "Too many requests", code = ErrorCodes.RateLimited }, ct);
        };
    });
}
