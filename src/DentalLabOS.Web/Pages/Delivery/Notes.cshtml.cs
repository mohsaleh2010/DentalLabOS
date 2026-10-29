using DentalLabOS.Application.Common;
using DentalLabOS.Application.Production;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Production;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DentalLabOS.Web.Pages.Delivery;

[Screen("ORD-22")]
[HasPermission(Permissions.Delivery.Read)]
public sealed class NotesModel(DeliveryService delivery) : AppPageModel
{
    public static readonly string[] Filters = ["active", "Delivered", "Failed", "Cancelled", "all"];

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "active";
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public PagedResult<DeliveryNoteListItemDto> Result { get; private set; } = default!;

    public string PageUrl => $"/delivery/notes?q={Uri.EscapeDataString(Q ?? string.Empty)}&status={Status}&page={{page}}";

    public async Task OnGetAsync()
    {
        Status = Filters.Contains(Status) ? Status : "active";
        Result = await delivery.ListNotesAsync(new PageQuery(PageNumber, 25, Q), Status == "all" ? null : Status);
    }
}
