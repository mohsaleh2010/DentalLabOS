using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Production;

[Screen("ORD-10")]
[HasPermission(Permissions.Production.Read)]
public sealed class BoardModel(ProductionService production) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Dept { get; set; }

    public BoardDto Board { get; private set; } = default!;
    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Departments = await production.DepartmentOptionsAsync();
        Board = await production.BoardAsync(Dept);
    }
}
