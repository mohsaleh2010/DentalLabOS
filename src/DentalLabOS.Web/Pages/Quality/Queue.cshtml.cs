using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Quality;

[Screen("ORD-17")]
[HasPermission(Permissions.Quality.Read)]
public sealed class QueueModel(QualityService quality, ProductionService production) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Dept { get; set; }

    public QualityOverviewDto Overview { get; private set; } = default!;
    public IReadOnlyList<QualityQueueItemDto> Items { get; private set; } = [];
    public IReadOnlyList<QualityCheckDto> Recent { get; private set; } = [];
    public IReadOnlyList<OptionDto> Departments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Overview = await quality.OverviewAsync();
        Items = await quality.QueueAsync(Dept);
        Recent = (await quality.RecentChecksAsync()).Take(8).ToList();
        Departments = await production.DepartmentOptionsAsync();
    }
}
