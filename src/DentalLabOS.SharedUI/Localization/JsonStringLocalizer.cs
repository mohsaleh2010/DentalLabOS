using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace DentalLabOS.SharedUI.Localization;

/// <summary>
/// Localizer backed by embedded JSON resource files (<c>Resources/{lang}.json</c>, flat "Area.Key" names).
/// JSON keeps the same resources usable by the web app, the API and a future MAUI client (ADR-0005).
/// Lookup order: current UI culture's language → Arabic (default) → the key itself.
/// </summary>
public sealed class JsonStringLocalizer : IStringLocalizer
{
    public const string DefaultLanguage = "ar";
    public static readonly string[] SupportedLanguages = ["ar", "en"];

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

    public LocalizedString this[string name]
    {
        get
        {
            var value = Find(name, out var found);
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = Find(name, out var found);
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, value, arguments), resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Load(CurrentLanguage).Select(kv => new LocalizedString(kv.Key, kv.Value));

    public static string CurrentLanguage
    {
        get
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return SupportedLanguages.Contains(lang) ? lang : DefaultLanguage;
        }
    }

    /// <summary>True when the key exists in the default (Arabic) resources — used by tests to find missing keys.</summary>
    public static bool HasKey(string language, string key) => Load(language).ContainsKey(key);

    public static IReadOnlyDictionary<string, string> Load(string language) => Cache.GetOrAdd(language, static lang =>
    {
        var assembly = typeof(JsonStringLocalizer).Assembly;
        using var stream = assembly.GetManifestResourceStream($"DentalLabOS.SharedUI.Resources.{lang}.json");
        if (stream is null)
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    });

    private static string Find(string name, out bool found)
    {
        if (Load(CurrentLanguage).TryGetValue(name, out var value) || Load(DefaultLanguage).TryGetValue(name, out value))
        {
            found = true;
            return value;
        }

        found = false;
        return name;
    }
}

public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private static readonly JsonStringLocalizer Instance = new();

    public IStringLocalizer Create(Type resourceSource) => Instance;

    public IStringLocalizer Create(string baseName, string location) => Instance;
}
