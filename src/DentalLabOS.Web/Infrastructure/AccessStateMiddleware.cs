using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>
/// Signs out sessions whose user was deactivated or whose lab was suspended, on the very next request
/// (plan 10.4: "انتهاء الجلسات وتغيير الصلاحيات").
/// </summary>
public sealed class AccessStateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PermissionService permissions, SignInManager<ApplicationUser> signIn)
    {
        if (context.User.Identity?.IsAuthenticated == true && !IsStatic(context.Request.Path))
        {
            var state = await permissions.GetAccessStateAsync(context.RequestAborted);
            if (state is AccessState.UserDisabled or AccessState.TenantSuspended)
            {
                await signIn.SignOutAsync();
                context.Response.Redirect($"/session-expired?reason={(state == AccessState.TenantSuspended ? "suspended" : "disabled")}");
                return;
            }
        }

        await next(context);
    }

    private static bool IsStatic(PathString path) =>
        path.StartsWithSegments("/_content") || path.StartsWithSegments("/css") || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/img") || path.StartsWithSegments("/session-expired") || path.Value is "/sw.js" or "/manifest.webmanifest";
}
