using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

[Screen("AUTH-07")]
public sealed class PasswordModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IAuditWriter audit, AppDbContext db) : AppPageModel
{
    [BindProperty] public string Current { get; set; } = string.Empty;
    [BindProperty] public string New { get; set; } = string.Empty;
    [BindProperty] public string Confirm { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (New != Confirm)
        {
            ModelState.AddModelError(nameof(Confirm), L["Validation.PasswordMismatch"]);
            return Page();
        }

        var user = await users.GetUserAsync(User) ?? throw new InvalidOperationException();
        var result = await users.ChangePasswordAsync(user, Current, New);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
            {
                ModelState.AddModelError(e.Code == "PasswordMismatch" ? string.Empty : nameof(New), L[e.Code == "PasswordMismatch" ? "Password.WrongCurrent" : $"Identity.{e.Code}"]);
            }

            return Page();
        }

        audit.Add(AuditActions.Update, "UserPassword", user.Id, tenantId: user.TenantId);
        await db.SaveChangesAsync();
        await signIn.RefreshSignInAsync(user);
        Flash("Password.Changed");
        return RedirectToPage();
    }
}
