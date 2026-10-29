using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts.Catalog;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

[Screen("ORD-23")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class RunsModel(DeliveryService delivery, LabCalendar calendar) : AppPageModel
{
    [BindProperty] public DateOnly RunDate { get; set; }
    [BindProperty] public Guid? CourierUserId { get; set; }
    [BindProperty] public string? CourierName { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public IReadOnlyList<DeliveryRunListItemDto> Items { get; private set; } = [];
    public IReadOnlyList<OptionDto> Couriers { get; private set; } = [];
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        RunDate = Today;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Can(Permissions.Delivery.Manage))
        {
            return Forbid();
        }

        Guid id = default;
        if (!await TryExecuteAsync(async () => id = await delivery.SaveRunAsync(null, new DeliveryRunRequest(RunDate, CourierUserId, CourierName, Notes)), string.Empty))
        {
            await LoadAsync();
            return Page();
        }

        Flash("Delivery.RunCreated");
        return Redirect($"/delivery/runs/{id}");
    }

    private async Task LoadAsync()
    {
        Today = await calendar.TodayAsync();
        Items = await delivery.ListRunsAsync(Today.AddDays(-14));
        if (Can(Permissions.Delivery.Manage))
        {
            Couriers = await delivery.CourierOptionsAsync();
        }
    }
}
