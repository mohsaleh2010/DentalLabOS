using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Account;

[Screen("AUTH-06")]
public sealed class SetupModel(PlatformBootstrapService bootstrap, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) : AppPageModel
{
    public bool IsOpen { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => IsOpen = await bootstrap.IsSetupOpenAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        IsOpen = await bootstrap.IsSetupOpenAsync();
        Guid userId = default;
        if (!await TryExecuteAsync(async () => userId = await bootstrap.CreateOwnerAsync(Input.Token, Input.FullName, Input.Email, Input.Password)))
        {
            Input.Password = string.Empty;
            return Page();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        await signIn.SignInAsync(user!, isPersistent: false);
        Flash("Auth.Setup.Done");
        return Redirect("/platform");
    }
}
