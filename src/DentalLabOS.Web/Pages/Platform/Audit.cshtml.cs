using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Platform;

[Screen("PLT-12")]
[HasPermission(Permissions.Platform.AuditRead)]
public sealed class AuditModel(AuditQueryService audit) : AppPageModel
{
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<AuditLogDto> Result { get; private set; } = default!;

    public string PageUrl => "/platform/audit?page={page}";

    public async Task OnGetAsync() => Result = await audit.ListForPlatformAsync(new AuditQuery(PageNumber, 30));
}
