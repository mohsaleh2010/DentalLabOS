using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DentalLabOS.Web.Pages.Common;

/// <summary>Switches UI language (cookie) and saves it to the signed-in user's profile.</summary>
public sealed class CultureModel(UserManager<ApplicationUser> users) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync(string culture, string? returnUrl)
    {
        if (UiPreferences.Cultures.All(c => c.Name != culture))
        {
            culture = "ar-EG";
        }

        UiPreferences.SetCultureCookie(Response, culture);
        if (User.Identity?.IsAuthenticated == true && await users.GetUserAsync(User) is { } user && user.PreferredCulture != culture)
        {
            user.PreferredCulture = culture;
            await users.UpdateAsync(user);
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
