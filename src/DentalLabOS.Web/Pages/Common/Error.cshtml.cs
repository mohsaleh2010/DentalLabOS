using System.Diagnostics;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Common;

/// <summary>SYS-07. Shows no internal details (plan 8.1); only a correlation reference for support.</summary>
[Screen("SYS-07")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : AppPageModel
{
    public int Code { get; private set; }
    public string Key => Code switch { 403 => "403", 404 => "404", 409 => "409", 503 => "503", _ => "500" };
    public string? RequestId { get; private set; }

    public void OnGet(int? code) => Set(code);

    public void OnPost(int? code) => Set(code);

    private void Set(int? code)
    {
        Code = code is >= 400 and < 600 ? code.Value : 500;
        if (Code >= 500)
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }

        // Keep the real status for re-executed errors; a redirected /error/403 page itself returns 200.
        if (HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() is not null)
        {
            Response.StatusCode = Code;
        }
    }
}
