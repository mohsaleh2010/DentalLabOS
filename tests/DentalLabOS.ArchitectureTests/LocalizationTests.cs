using System.Text.Json;
using System.Text.RegularExpressions;

namespace DentalLabOS.ArchitectureTests;

public sealed partial class LocalizationTests
{
    private static Dictionary<string, string> Load(string lang) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Repo.Path("src", "DentalLabOS.SharedUI", "Resources", $"{lang}.json")))!;

    [GeneratedRegex("L\\[\"([A-Z][A-Za-z0-9]*(?:\\.[A-Za-z0-9_]+)+)\"")]
    private static partial Regex LiteralKey();

    [GeneratedRegex("\"((?:Validation|Errors|Setup|Roadmap|Nav)\\.[A-Za-z0-9_.]+)\"")]
    private static partial Regex KeyInCode();

    [Fact]
    public void Arabic_and_English_have_the_same_keys()
    {
        var ar = Load("ar").Keys.ToHashSet();
        var en = Load("en").Keys.ToHashSet();
        Assert.Empty(ar.Except(en));
        Assert.Empty(en.Except(ar));
    }

    [Fact]
    public void No_empty_translations()
    {
        foreach (var lang in new[] { "ar", "en" })
        {
            Assert.Empty(Load(lang).Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key));
        }
    }

    [Fact]
    public void Every_literal_key_used_in_views_and_code_exists()
    {
        var ar = Load("ar");
        var missing = Repo.SourceFiles("*.cshtml").Concat(Repo.SourceFiles("*.cs")).Concat(Repo.SourceFiles("*.razor"))
            .SelectMany(f =>
            {
                var text = File.ReadAllText(f);
                return LiteralKey().Matches(text).Concat(KeyInCode().Matches(text)).Select(m => (File: Repo.Relative(f), Key: m.Groups[1].Value));
            })
            .Where(x => !x.Key.EndsWith('.') && !ar.ContainsKey(x.Key))
            .Distinct()
            .ToArray();

        Assert.True(missing.Length == 0, "Missing keys: " + string.Join(", ", missing.Select(u => $"{u.Key} ({u.File})")));
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        var ar = Load("ar");
        var en = Load("en");
        foreach (var (key, value) in en)
        {
            var enArgs = Regex.Matches(value, "\\{\\d\\}").Select(m => m.Value).Order();
            var arArgs = Regex.Matches(ar[key], "\\{\\d\\}").Select(m => m.Value).Order();
            Assert.True(enArgs.SequenceEqual(arArgs), $"{key}: placeholders differ");
        }
    }
}
