using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Catalog;

[Screen("ADM-12")]
[HasPermission(Permissions.Admin.TaxesManage)]
public sealed class TaxesModel(ReferenceDataService reference) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<TaxCodeDto> TaxCodes { get; private set; } = [];

    public sealed class InputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public bool IsWithholding { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        TaxCodes = await reference.ListTaxCodesAsync();
        if (Edit is { } id && TaxCodes.FirstOrDefault(t => t.Id == id) is { } t)
        {
            Input = new InputModel { Code = t.Code, Name = t.Name, Rate = t.Rate, IsWithholding = t.IsWithholding, IsActive = t.IsActive, RowVersion = t.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new TaxCodeUpsertRequest(Input.Code, Input.Name, Input.Rate, Input.IsWithholding, Edit is null || Input.IsActive, Input.RowVersion);
        if (!await TryExecuteAsync(() => reference.SaveTaxCodeAsync(Edit, request)))
        {
            TaxCodes = await reference.ListTaxCodesAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/taxes");
    }
}
