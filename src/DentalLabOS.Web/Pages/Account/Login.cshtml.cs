using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

[Screen("AUTH-01")]
public sealed class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AppDbContext db, IAuditWriter audit, IClock clock)
    : AppPageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/") : Page();

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Input.Email) || string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError(string.Empty, L["Auth.Login.Invalid"]);
            return Page();
        }

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user!.LastLoginAtUtc = clock.UtcNow;
            await users.UpdateAsync(user);
            audit.Add(AuditActions.Login, "User", user.Id, tenantId: user.TenantId);
            await db.SaveChangesAsync();
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }

        if (user is not null)
        {
            audit.Add(AuditActions.LoginFailed, "User", user.Id, result.IsLockedOut ? "LockedOut" : result.IsNotAllowed ? "NotAllowed" : "BadPassword", tenantId: user.TenantId);
            await db.SaveChangesAsync();
        }

        // One generic message for unknown e-mail / wrong password to avoid account enumeration.
        ModelState.AddModelError(string.Empty, L[result.IsLockedOut ? "Auth.Login.LockedOut" : result.IsNotAllowed ? "Auth.Login.NotAllowed" : "Auth.Login.Invalid"]);
        Input.Password = string.Empty;
        return Page();
    }
}
