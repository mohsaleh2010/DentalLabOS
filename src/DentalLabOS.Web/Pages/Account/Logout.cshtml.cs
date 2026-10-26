using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Operations;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DentalLabOS.Web.Pages.Account;

public sealed class LogoutModel(SignInManager<ApplicationUser> signIn, ICurrentUser currentUser, IAuditWriter audit, AppDbContext db) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        if (currentUser.UserId is { } id)
        {
            audit.Add(AuditActions.Logout, "User", id);
            await db.SaveChangesAsync();
        }

        await signIn.SignOutAsync();
        return Redirect("/login");
    }
}
