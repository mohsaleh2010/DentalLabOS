using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Production;

[Screen("ORD-29")]
[HasPermission(Permissions.Production.Read)]
public sealed class CalendarModel(ProductionService production, LabCalendar calendar) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Month { get; set; }

    public CalendarDto Data { get; private set; } = default!;
    public DateOnly First { get; private set; }
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        Today = await calendar.TodayAsync();
        First = DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", out var m) ? m : new DateOnly(Today.Year, Today.Month, 1);
        Data = await production.CalendarAsync(First.Year, First.Month);
    }
}
