using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

/// <summary>SYS-02: personal appearance and language. Customization never touches permissions (plan 8.1).</summary>
[Screen("SYS-02")]
public sealed class PreferencesModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : AppPageModel
{
    [BindProperty]
    public string Theme { get; set; } = "system";

    [BindProperty]
    public string Culture { get; set; } = "ar-EG";

    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User) ?? throw new InvalidOperationException();
        Theme = user.Theme;
        Culture = user.PreferredCulture;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User) ?? throw new InvalidOperationException();
        user.Theme = UiPreferences.Themes.Contains(Theme) ? Theme : "system";
        user.PreferredCulture = UiPreferences.Cultures.Any(c => c.Name == Culture) ? Culture : "ar-EG";
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);

        Response.Cookies.Append(UiPreferences.ThemeCookie, user.Theme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), SameSite = SameSiteMode.Lax });
        UiPreferences.SetCultureCookie(Response, user.PreferredCulture);
        Flash("Common.Saved");
        return RedirectToPage();
    }
}
