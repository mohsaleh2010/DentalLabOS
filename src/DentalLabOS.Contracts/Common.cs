namespace DentalLabOS.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Server-side paging/search/sort parameters. Page size is clamped to <see cref="MaxPageSize"/>.</summary>
public sealed record PageQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null)
{
    public const int MaxPageSize = 100;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
    public int Skip => (SafePage - 1) * SafePageSize;
}

/// <summary>Stable error codes returned in ProblemDetails "code" extension.</summary>
public static class ErrorCodes
{
    public const string Validation = "validation_failed";
    public const string NotFound = "not_found";
    public const string Forbidden = "forbidden";
    public const string Conflict = "concurrency_conflict";
    public const string BusinessRule = "business_rule";
    public const string Unauthorized = "unauthorized";
    public const string RateLimited = "rate_limited";
}

public sealed record HealthResponse(string Status, string Version, DateTime ServerTimeUtc, bool Database);
