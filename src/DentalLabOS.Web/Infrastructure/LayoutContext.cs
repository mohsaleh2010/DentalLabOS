using DentalLabOS.Application.Abstractions;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Per-request data for the app shell (lab name, plan, user display).</summary>
public sealed class LayoutContext(AppDbContext db, ICurrentUser currentUser, IHttpContextAccessor accessor, IPermissionService permissions)
{
    public sealed record Info(string? LabName, string? PlanName, DateOnly? PlanEnds, bool IsTrial, string UserName, string Initials, bool IsPlatform);

    private Info? _info;

    public async Task<Info> GetAsync()
    {
        if (_info is not null)
        {
            return _info;
        }

        var name = accessor.HttpContext?.User.FindFirst(AppClaimTypes.FullName)?.Value ?? string.Empty;
        string? lab = null, plan = null;
        DateOnly? ends = null;
        var trial = false;
        if (currentUser.TenantId is { } tid)
        {
            var t = await db.Tenants.AsNoTracking().Where(x => x.Id == tid)
                .Select(x => new
                {
                    x.Name,
                    x.Status,
                    Plan = x.Subscriptions.Where(s => s.IsCurrent).Select(s => new { s.Plan.Name, s.EndDate }).FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
            lab = t?.Name;
            plan = t?.Plan?.Name;
            ends = t?.Plan?.EndDate;
            trial = t?.Status == Domain.Common.TenantStatus.Trial;
        }

        return _info = new Info(lab, plan, ends, trial, name, Initials(name), currentUser.IsPlatformStaff && currentUser.TenantId is null);
    }

    public Task<IReadOnlySet<string>> GrantedAsync() => permissions.GetGrantedAsync();

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1],
            _ => $"{parts[0][0]}{parts[^1][0]}",
        };
    }
}
