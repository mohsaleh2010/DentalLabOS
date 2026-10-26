using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Admin;

[Screen("ADM-25")]
[HasPermission(Permissions.Admin.AuditRead)]
public sealed class AuditModel(AuditQueryService audit) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<AuditLogDto> Result { get; private set; } = default!;

    public string PageUrl => $"/admin/audit?action={Action}&from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&page={{page}}";

    public async Task OnGetAsync() => Result = await audit.ListForTenantAsync(new AuditQuery(PageNumber, 30, Action, null, From, To));
}
