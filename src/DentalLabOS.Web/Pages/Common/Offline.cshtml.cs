using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Common;

/// <summary>SYS-06: the service worker's fallback page. Contains no user or lab data so it is safe to cache.</summary>
[Screen("SYS-06")]
public sealed class OfflineModel : AppPageModel
{
    public void OnGet()
    {
    }
}
