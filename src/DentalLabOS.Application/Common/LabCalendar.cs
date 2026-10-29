using DentalLabOS.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Common;

/// <summary>Dates in the lab's own time zone (due dates and "overdue" are local, not UTC).</summary>
public sealed class LabCalendar(IApplicationDbContext db, ITenantContext tenant, IClock clock)
{
    private TimeZoneInfo? _zone;

    public async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct = default)
    {
        if (_zone is not null)
        {
            return _zone;
        }

        var id = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.TimeZoneId).FirstOrDefaultAsync(ct);
        _zone = id is not null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;
        return _zone;
    }

    public async Task<DateTime> NowLocalAsync(CancellationToken ct = default) =>
        TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, await ZoneAsync(ct));

    public async Task<DateOnly> TodayAsync(CancellationToken ct = default) => DateOnly.FromDateTime(await NowLocalAsync(ct));

    /// <summary>Suggested due date from a service's standard lead time (8 working hours a day, at least one day).</summary>
    public static DateOnly SuggestDueDate(DateOnly today, decimal leadHours) =>
        today.AddDays(Math.Max(1, (int)Math.Ceiling(leadHours / 8m)));
}
