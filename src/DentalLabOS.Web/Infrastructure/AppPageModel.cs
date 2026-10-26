using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Base page model: localized validation mapping, flash toasts and permission checks for handlers.</summary>
public abstract class AppPageModel : PageModel
{
    /// <summary>Error keys that don't belong to a single input and are shown in the page-level summary.</summary>
    private static readonly HashSet<string> GeneralFields = new(StringComparer.Ordinal) { "Token", "User", "Roles", "Permissions" };

    protected IStringLocalizer L => HttpContext.RequestServices.GetRequiredService<IStringLocalizer>();

    protected IPermissionService PermissionService => HttpContext.RequestServices.GetRequiredService<IPermissionService>();

    /// <summary>Runs a command; maps validation/concurrency failures to ModelState and returns false.</summary>
    protected async Task<bool> TryExecuteAsync(Func<Task> action, string prefix = "Input")
    {
        try
        {
            await action();
            return true;
        }
        catch (ValidationException ex)
        {
            foreach (var (field, keys) in ex.Errors)
            {
                foreach (var key in keys)
                {
                    var modelKey = GeneralFields.Contains(field) ? string.Empty : string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
                    ModelState.AddModelError(modelKey, L[key]);
                }
            }

            return false;
        }
        catch (ConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, L["Errors.Concurrency"]);
            return false;
        }
    }

    protected void Flash(string messageKey, params object[] args) => TempData["toast"] = L[messageKey, args].Value;

    protected void FlashError(string messageKey) => TempData["toast-error"] = L[messageKey].Value;
}
