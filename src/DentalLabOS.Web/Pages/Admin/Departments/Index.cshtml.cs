using DentalLabOS.Application.Organization;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Departments;

[Screen("ADM-04")]
[HasPermission(Permissions.Admin.DepartmentsManage)]
public sealed class IndexModel(DepartmentService departments) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<DepartmentDto> Result { get; private set; } = default!;

    public string PageUrl => $"/admin/departments?q={Uri.EscapeDataString(Q ?? string.Empty)}&page={{page}}";

    public async Task OnGetAsync() => Result = await departments.ListAsync(new PageQuery(PageNumber, 20, Q));
}
