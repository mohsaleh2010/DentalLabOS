using DentalLabOS.Application.Organization;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin.Branches;

[Screen("ADM-03")]
[HasPermission(Permissions.Admin.BranchesManage)]
public sealed class IndexModel(BranchService branches) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<BranchDto> Result { get; private set; } = default!;

    public string PageUrl => $"/admin/branches?q={Uri.EscapeDataString(Q ?? string.Empty)}&page={{page}}";

    public async Task OnGetAsync() => Result = await branches.ListAsync(new PageQuery(PageNumber, 20, Q));
}
