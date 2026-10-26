using DentalLabOS.Web.Infrastructure;

namespace DentalLabOS.Web.Pages.Common;

/// <summary>NTF-01 shell; in-app notifications are produced from phase 3 (delays, missing data, readiness).</summary>
[Screen("NTF-01")]
public sealed class NotificationsModel : AppPageModel
{
    public void OnGet()
    {
    }
}
