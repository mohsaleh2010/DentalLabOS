using DentalLabOS.Application.Abstractions;

namespace DentalLabOS.Application.Common;

/// <summary>Shared rules for master-data codes and enum inputs.</summary>
public static class MasterData
{
    /// <summary>Letters, digits and <c>_ . -</c>; stored upper-case.</summary>
    public const string CodePattern = "^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$";

    public static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Parses an enum by name only (numbers and undefined values are rejected).</summary>
    public static bool TryParseEnum<T>(string? value, out T result)
        where T : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
               && !char.IsDigit(value.Trim()[0])
               && Enum.TryParse(value.Trim(), true, out result)
               && Enum.IsDefined(result);
    }

    /// <summary>
    /// Next number from the lab's master-data sequence, skipping numbers someone already typed in by hand.
    /// Gaps are acceptable here: unlike documents, master-data codes are not fiscal sequences.
    /// </summary>
    public static async Task<string> NextCodeAsync(INumberingService numbering, string sequence, DateOnly date, Func<string, Task<bool>> exists, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            var code = await numbering.NextAsync(sequence, null, date, ct);
            if (!await exists(code))
            {
                return code;
            }
        }

        throw ValidationException.For("Code", "Validation.CodeTaken");
    }
}
