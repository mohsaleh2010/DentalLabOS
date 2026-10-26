using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Catalog;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace DentalLabOS.Web.Pages.Items;

[Screen("ITM-11")]
[HasPermission(Permissions.Items.Import)]
public sealed class ImportModel(ImportService imports, IMemoryCache cache, ICurrentUser currentUser) : ImportPageModel(imports, cache, currentUser)
{
    public override ImportKind Kind => ImportKind.Items;

    public void OnGet()
    {
    }
}
