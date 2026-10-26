using System.Security.Claims;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Common;
using DentalLabOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace DentalLabOS.Infrastructure.Tenancy;

/// <summary>Reads the signed-in user from the current HTTP request (cookie or bearer token).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? TenantId => IsAuthenticated && Guid.TryParse(Principal?.FindFirstValue(AppClaimTypes.TenantId), out var id) ? id : null;

    public PlatformRole PlatformRole =>
        IsAuthenticated && Enum.TryParse<PlatformRole>(Principal?.FindFirstValue(AppClaimTypes.PlatformRole), out var r) ? r : PlatformRole.None;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}

/// <summary>
/// Scoped tenant context. The tenant comes from the user's claims and can only be overridden by
/// <see cref="BeginScope"/> (used by platform provisioning and invitation acceptance). Nested scopes restore on dispose.
/// </summary>
public sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    private readonly Stack<Guid> _overrides = new();

    public Guid? TenantId => _overrides.Count > 0 ? _overrides.Peek() : currentUser.TenantId;

    public bool IsPlatformContext => _overrides.Count == 0 && currentUser.TenantId is null && currentUser.IsPlatformStaff;

    public IDisposable BeginScope(Guid tenantId)
    {
        _overrides.Push(tenantId);
        return new Pop(_overrides);
    }

    private sealed class Pop(Stack<Guid> stack) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                stack.Pop();
                _disposed = true;
            }
        }
    }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
