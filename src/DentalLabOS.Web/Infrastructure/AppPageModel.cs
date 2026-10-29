using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

    /// <summary>The signed-in user's permissions, used to show or hide actions (the server still checks each one).</summary>
    public IReadOnlySet<string> Granted { get; private set; } = new HashSet<string>();

    public bool Can(string permission) => Granted.Contains(permission);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            Granted = await PermissionService.GetGrantedAsync();
        }

        await next();
    }

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
        catch (DentalLabOS.Domain.Common.DomainException ex)
        {
            ModelState.AddModelError(string.Empty, L[ex.MessageKey]);
            return false;
        }
    }

    /// <summary>
    /// Runs a one-click action (start, complete, release…) and redirects back: success as a toast,
    /// a rule or validation failure as an error toast. Permission and not-found failures still go to the status pages.
    /// </summary>
    protected async Task<IActionResult> ActAsync(Func<Task> action, string successKey, string redirectUrl)
    {
        try
        {
            await action();
            Flash(successKey);
        }
        catch (DentalLabOS.Domain.Common.DomainException ex)
        {
            FlashError(ex.MessageKey);
        }
        catch (ValidationException ex)
        {
            FlashError(ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Errors.Validation");
        }
        catch (ConcurrencyException)
        {
            FlashError("Errors.Concurrency");
        }

        return Redirect(redirectUrl);
    }

    /// <summary>Localizes a coded message such as <c>WorkOrder.PossibleDuplicate:WO-26-000012</c> (key, then arguments).</summary>
    public string Coded(string coded)
    {
        var parts = coded.Split(':');
        return L[parts[0], parts.Skip(1).Cast<object>().ToArray()].Value;
    }

    /// <summary>Only local paths are accepted as a return address.</summary>
    protected static string LocalOr(string? url, string fallback) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal) ? url : fallback;

    protected void Flash(string messageKey, params object[] args) => TempData["toast"] = L[messageKey, args].Value;

    protected void FlashError(string messageKey) => TempData["toast-error"] = L[messageKey].Value;
}
