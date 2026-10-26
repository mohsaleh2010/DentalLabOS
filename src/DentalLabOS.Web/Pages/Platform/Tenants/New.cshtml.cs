using DentalLabOS.Application.Platform;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Platform.Tenants;

[Screen("PLT-03")]
[HasPermission(Permissions.Platform.TenantsCreate)]
public sealed class NewModel(PlatformTenantService tenants) : AppPageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<PlanDto> Plans { get; private set; } = [];

    public CreateTenantResult? Created { get; private set; }

    public string InviteUrl => Created is null ? string.Empty : $"{Request.Scheme}://{Request.Host}/invite/accept?token={Created.OwnerInviteToken}";

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string MainBranchName { get; set; } = "الفرع الرئيسي";
        public string DefaultCulture { get; set; } = "ar-EG";
        public string BaseCurrencyCode { get; set; } = "EGP";
        public Guid PlanId { get; set; }
        public int TrialDays { get; set; } = 14;
        public string OwnerFullName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => Plans = await tenants.ListPlansAsync(activeOnly: true);

    public async Task<IActionResult> OnPostAsync()
    {
        Plans = await tenants.ListPlansAsync(activeOnly: true);
        var request = new CreateTenantRequest(Input.Name, Input.Slug, Input.OwnerFullName, Input.OwnerEmail, Input.PlanId, Input.TrialDays,
            Input.DefaultCulture, Input.BaseCurrencyCode, "Africa/Cairo", Input.ContactPhone,
            string.IsNullOrWhiteSpace(Input.MainBranchName) ? L["Defaults.MainBranch"] : Input.MainBranchName);

        // The invitation link is shown once here; only its hash is stored.
        await TryExecuteAsync(async () => Created = await tenants.CreateTenantAsync(request));
        return Page();
    }
}
