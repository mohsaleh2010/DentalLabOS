using DentalLabOS.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Requires a permission key (plan A3.1). Checked on the server for every request.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.For(permission))
{
    public string Permission { get; } = permission;
}

/// <summary>Links a page to its machine-readable spec in docs/screens/{Id}.screen.json (plan section C).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScreenAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
