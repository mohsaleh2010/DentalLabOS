using DentalLabOS.Application.Abstractions;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Web.Pages.Account;

[Screen("SYS-01")]
public sealed class ProfileModel(UserManager<ApplicationUser> users, AppDbContext db, ICurrentUser currentUser) : AppPageModel
{
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public string? LabName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public DateTime? LastLogin { get; private set; }
    public int PermissionCount { get; private set; }

    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User) ?? throw new InvalidOperationException();
        FullName = user.FullName;
        Email = user.Email ?? string.Empty;
        Initials = string.Concat(user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]));
        LastLogin = user.LastLoginAtUtc;
        PermissionCount = (await PermissionService.GetGrantedAsync()).Count;
        if (currentUser.TenantId is { } tid)
        {
            LabName = await db.Tenants.Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefaultAsync();
            Roles = await db.UserProfiles.Where(p => p.UserId == user.Id).SelectMany(p => p.Roles).Select(r => r.Role.Name).ToListAsync();
        }
        else
        {
            Roles = [L[$"PlatformRole.{user.PlatformRole}"]];
        }
    }
}
