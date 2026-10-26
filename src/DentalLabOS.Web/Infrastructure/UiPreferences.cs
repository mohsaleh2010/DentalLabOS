using System.Globalization;
using DentalLabOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Localization;

namespace DentalLabOS.Web.Infrastructure;

public static class UiPreferences
{
    public const string ThemeCookie = "dlos-theme";
    public static readonly string[] Themes = ["light", "dark", "system"];
    public static readonly CultureInfo[] Cultures = [WithLatinNumbers("ar-EG"), new("en-US")];

    /// <summary>
    /// Arabic UI with Latin digits and "." decimals, so amounts look the same in both languages and number inputs
    /// (which always post "12.5") bind correctly.
    /// </summary>
    private static CultureInfo WithLatinNumbers(string name)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        var n = culture.NumberFormat;
        n.NumberDecimalSeparator = n.CurrencyDecimalSeparator = n.PercentDecimalSeparator = ".";
        n.NumberGroupSeparator = n.CurrencyGroupSeparator = n.PercentGroupSeparator = ",";
        n.NegativeSign = "-";
        n.PositiveSign = "+";
        n.DigitSubstitution = DigitShapes.None;
        return culture;
    }

    public static string Theme(HttpContext ctx)
    {
        var cookie = ctx.Request.Cookies[ThemeCookie];
        if (cookie is not null && Themes.Contains(cookie))
        {
            return cookie;
        }

        var claim = ctx.User.FindFirst(AppClaimTypes.Theme)?.Value;
        return claim is not null && Themes.Contains(claim) ? claim : "system";
    }

    public static bool IsRtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    public static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public static void SetCultureCookie(HttpResponse response, string culture) =>
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true });

    /// <summary>Falls back to the signed-in user's saved language when no culture cookie exists.</summary>
    public sealed class ClaimCultureProvider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            var culture = httpContext.User.FindFirst(AppClaimTypes.Culture)?.Value;
            return Task.FromResult(culture is null ? null : new ProviderCultureResult(culture));
        }
    }
}
