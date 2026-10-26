using DentalLabOS.Infrastructure.Security;

namespace DentalLabOS.Api.Infrastructure;

/// <summary>
/// Rejects still-valid tokens of users who were deactivated or whose lab was suspended (checked on every request),
/// so access ends immediately rather than when the token expires.
/// </summary>
public sealed class AccessStateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PermissionService permissions)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var state = await permissions.GetAccessStateAsync(context.RequestAborted);
            if (state is AccessState.UserDisabled or AccessState.TenantSuspended)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { status = 401, title = "Access revoked", code = state == AccessState.TenantSuspended ? "tenant_suspended" : "user_disabled" },
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
