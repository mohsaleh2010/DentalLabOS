using System.Globalization;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Display formats shared by all pages (numbers keep Latin digits in both languages).</summary>
public static class Fmt
{
    public static string Money(decimal value) => value.ToString("#,0.00", CultureInfo.CurrentCulture);

    public static string Money(decimal? value) => value is { } v ? Money(v) : "—";

    public static string Qty(decimal value) => value.ToString("#,0.######", CultureInfo.CurrentCulture);

    public static string Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    public static string Input(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Input(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
