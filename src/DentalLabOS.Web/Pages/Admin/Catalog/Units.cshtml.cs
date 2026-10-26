using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Catalog;

[Screen("ADM-14")]
[HasPermission(Permissions.Admin.UnitsManage)]
public sealed class UnitsModel(ReferenceDataService reference) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Edit { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IReadOnlyList<UnitDto> Units { get; private set; } = [];

    public sealed class InputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; } = 2;
        public string? RowVersion { get; set; }
    }

    public async Task OnGetAsync()
    {
        Units = await reference.ListUnitsAsync();
        if (Edit is { } id && Units.FirstOrDefault(u => u.Id == id) is { } u)
        {
            Input = new InputModel { Code = u.Code, Name = u.Name, DecimalPlaces = u.DecimalPlaces, RowVersion = u.RowVersion };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await TryExecuteAsync(() => reference.SaveUnitAsync(Edit, new UnitUpsertRequest(Input.Code, Input.Name, Input.DecimalPlaces, Input.RowVersion))))
        {
            Units = await reference.ListUnitsAsync();
            return Page();
        }

        Flash("Common.Saved");
        return Redirect("/admin/units");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid unitId)
    {
        if (await TryExecuteAsync(() => reference.DeleteUnitAsync(unitId)))
        {
            Flash("Common.Deleted");
        }
        else
        {
            FlashError("Validation.InUse");
        }

        return Redirect("/admin/units");
    }
}
