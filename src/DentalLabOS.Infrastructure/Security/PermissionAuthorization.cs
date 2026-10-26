using DentalLabOS.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DentalLabOS.Infrastructure.Security;

/// <summary>Policy names of the form "perm:{key}" (plan A3.1). Shared by the web app and the API.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string For(string permission) => Prefix + permission;

    /// <summary>Registers on-demand permission policies resolved per request from the database.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        return services;
    }
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler(IPermissionService permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && await permissions.HasAsync(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Creates "perm:{key}" policies on demand so endpoints don't need policy registration.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
