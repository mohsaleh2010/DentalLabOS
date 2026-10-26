using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DentalLabOS.Web.Pages.Admin;

[Screen("ADM-23")]
[HasPermission(Permissions.Admin.ImportManage)]
public sealed class ImportModel(ImportService imports, IMemoryCache cache, ICurrentUser currentUser) : ImportPageModel(imports, cache, currentUser)
{
    [BindProperty(SupportsGet = true, Name = "kind")] public string? KindName { get; set; }

    public override ImportKind Kind => string.Equals(KindName, "items", StringComparison.OrdinalIgnoreCase) ? ImportKind.Items : ImportKind.Customers;

    public void OnGet()
    {
    }
}
