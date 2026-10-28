using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Infrastructure.Services;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

/// <summary>AUTH-08: pick the working branch. The choice is only a preference; every service re-checks the user's branch scope.</summary>
[Screen("AUTH-08")]
[Authorize]
public sealed class ContextModel(ProductionSetupService setup) : AppPageModel
{
    public IReadOnlyList<BranchOptionDto> Branches { get; private set; } = [];
    public Guid? Current { get; private set; }

    public async Task OnGetAsync()
    {
        Branches = await setup.MyBranchesAsync();
        Current = Guid.TryParse(Request.Cookies[HttpBranchSelection.Cookie], out var id) && Branches.Any(b => b.Id == id) ? id : null;
    }

    public async Task<IActionResult> OnPostAsync(Guid? branchId, string? returnUrl)
    {
        var branches = await setup.MyBranchesAsync();
        if (branchId is { } id && branches.Any(b => b.Id == id))
        {
            Response.Cookies.Append(HttpBranchSelection.Cookie, id.ToString(), new CookieOptions
            {
                HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365), IsEssential = true,
            });
            Flash("Context.Changed", branches.First(b => b.Id == id).Name);
        }
        else
        {
            Response.Cookies.Delete(HttpBranchSelection.Cookie);
            Flash("Context.Cleared");
        }

        return Redirect(LocalOr(returnUrl, "/"));
    }
}
