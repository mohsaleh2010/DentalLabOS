using DentalLabOS.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Common;

public static class PagingExtensions
{
    /// <summary>Counts and pages in SQL Server (never in memory).</summary>
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.SafePage, page.SafePageSize, total);
    }

    public static string? NormalizedSearch(this PageQuery page) =>
        string.IsNullOrWhiteSpace(page.Search) ? null : page.Search.Trim();
}
