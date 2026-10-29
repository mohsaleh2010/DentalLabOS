using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.ReasonCodes;

[Screen("ADM-18")]
[HasPermission(Permissions.Admin.ReasonsManage)]
public sealed class IndexModel(ProductionSetupService setup) : AppPageModel
{
    public static readonly string[] Scopes = ["Hold", "Cancellation", "Rejection", "Adjustment", "Remake", "DeliveryFailure", "Waste", "StockAdjustment"];
    public static readonly string[] Categories = ["DentistData", "Design", "Manufacturing", "Material", "Transport", "Unspecified"];

    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }
    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<ReasonCodeDto> Items { get; private set; } = [];

    public sealed class InputModel
    {
        public string Scope { get; set; } = "Hold";
        public string Category { get; set; } = "Unspecified";
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Edit is { } id && Items.FirstOrDefault(r => r.Id == id) is { } r)
        {
            Input = new InputModel { Scope = r.Scope, Category = r.Category, Name = r.Name, IsActive = r.IsActive, RowVersion = r.RowVersion };
        }
        else if (Scopes.Contains(Scope))
        {
            Input.Scope = Scope!;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var i = Input;
        if (!await TryExecuteAsync(() => setup.SaveReasonAsync(Edit, new ReasonCodeUpsertRequest(i.Scope, i.Category, i.Name, i.IsActive, i.RowVersion))))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect($"/admin/reason-codes?scope={i.Scope}");
    }

    private async Task LoadAsync()
    {
        Scope = Scopes.Contains(Scope) ? Scope : null;
        Items = await setup.ListReasonsAsync(Scope);
    }
}
