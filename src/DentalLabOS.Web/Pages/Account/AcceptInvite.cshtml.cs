using DentalLabOS.Application.Admin;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

[Screen("AUTH-05")]
public sealed class AcceptInviteModel(InvitationService invitations) : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public InvitationPreview? Preview { get; private set; }

    public async Task OnGetAsync() => Preview = await invitations.PreviewAsync(Token);

    public async Task<IActionResult> OnPostAsync()
    {
        Preview = await invitations.PreviewAsync(Token);
        if (Preview is null)
        {
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            ModelState.AddModelError(nameof(ConfirmPassword), L["Validation.PasswordMismatch"]);
            return Page();
        }

        if (!await TryExecuteAsync(() => invitations.AcceptAsync(Token, Password), prefix: string.Empty))
        {
            return Page();
        }

        return Redirect("/login?reason=invited");
    }
}
