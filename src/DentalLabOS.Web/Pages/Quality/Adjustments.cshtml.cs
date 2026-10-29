using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Quality;

[Screen("ORD-19")]
[HasPermission(Permissions.Quality.Read)]
public sealed class AdjustmentsModel(QualityService quality) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "open";

    public IReadOnlyList<AdjustmentDto> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Show = Show == "all" ? "all" : "open";
        Items = await quality.AdjustmentsAsync(Show == "open");
    }
}
