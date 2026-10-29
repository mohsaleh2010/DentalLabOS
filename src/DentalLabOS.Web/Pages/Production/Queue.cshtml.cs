using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Production;

[Screen("ORD-11")]
[HasPermission(Permissions.Production.Read)]
public sealed class QueueModel(ProductionService production) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid Dept { get; set; }

    public OptionDto? Department { get; private set; }
    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];
    public IReadOnlyList<TaskDto> Tasks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Departments = await production.DepartmentOptionsAsync();
        Department = Departments.FirstOrDefault(d => d.Id == Dept);
        if (Department is null)
        {
            return Departments.Count > 0 ? Redirect($"/production/queue/{Departments[0].Id}") : NotFound();
        }

        Tasks = await production.QueueAsync(Dept);
        return Page();
    }
}
