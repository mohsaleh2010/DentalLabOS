using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Common;

/// <summary>SYS-05 placeholder: global search goes live with customers and work orders (phases 2–3).</summary>
[Screen("SYS-05")]
public sealed class SearchModel : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public void OnGet()
    {
    }
}
