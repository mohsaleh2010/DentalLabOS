using System.Text.Json.Nodes;
using DentalLabOS.Domain.Security;
using DentalLabOS.Web.Infrastructure;
using Json.Schema;

namespace DentalLabOS.ArchitectureTests;

/// <summary>Plan C6: every page has a valid spec, and every permission a spec names exists in the catalog.</summary>
public sealed class ScreenRegistryTests
{
    private static readonly string ScreensDir = Repo.Path("docs", "screens");

    private static IEnumerable<string> SpecFiles() => Directory.EnumerateFiles(ScreensDir, "*.screen.json");

    private static JsonNode Load(string file) => JsonNode.Parse(File.ReadAllText(file))!;

    private static string[] ScreenIdsInCode() =>
        typeof(AppPageModel).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(ScreenAttribute), false).Cast<ScreenAttribute>())
            .Select(a => a.Id)
            .Distinct()
            .Order()
            .ToArray();

    [Fact]
    public void Every_spec_is_valid_against_the_schema()
    {
        var schema = JsonSchema.FromFile(Path.Combine(ScreensDir, "_schema", "screen.schema.json"));
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        foreach (var file in SpecFiles())
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            var result = schema.Evaluate(doc.RootElement, options);
            var errors = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"));
            Assert.True(result.IsValid, $"{Path.GetFileName(file)}: {string.Join("; ", errors)}");
        }
    }

    [Fact]
    public void File_name_matches_spec_id()
    {
        foreach (var file in SpecFiles())
        {
            Assert.Equal(Path.GetFileName(file), $"{Load(file)["id"]!.GetValue<string>()}.screen.json");
        }
    }

    [Fact]
    public void Every_screen_in_code_has_a_spec()
    {
        var specIds = SpecFiles().Select(f => Path.GetFileName(f).Replace(".screen.json", string.Empty, StringComparison.Ordinal)).ToHashSet();
        var missing = ScreenIdsInCode().Where(id => !specIds.Contains(id)).ToArray();
        Assert.True(missing.Length == 0, $"Pages without docs/screens spec: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Implemented_specs_correspond_to_a_page()
    {
        var inCode = ScreenIdsInCode().ToHashSet();
        var orphans = SpecFiles().Select(Load)
            .Where(s => s["status"]?.GetValue<string>() == "implemented")
            .Select(s => s["id"]!.GetValue<string>())
            .Where(id => !inCode.Contains(id))
            .ToArray();
        Assert.Empty(orphans);
    }

    [Fact]
    public void Spec_permissions_exist_in_the_catalog()
    {
        var catalog = Permissions.All.ToHashSet();
        foreach (var file in SpecFiles())
        {
            var perms = Load(file)["permissions"]!;
            var keys = new List<string?> { perms["view"]?.GetValue<string>() };
            if (perms["actions"] is JsonObject actions)
            {
                keys.AddRange(actions.Select(a => a.Value!.GetValue<string>()));
            }

            foreach (var key in keys.Where(k => k is not null))
            {
                Assert.True(catalog.Contains(key!), $"{Path.GetFileName(file)}: unknown permission '{key}'");
            }
        }
    }

    [Fact]
    public void Page_permission_matches_spec_view_permission()
    {
        var specs = SpecFiles().Select(Load).ToDictionary(s => s["id"]!.GetValue<string>(), s => s["permissions"]!);
        var pages = typeof(AppPageModel).Assembly.GetTypes()
            .Select(t => (Type: t, Screen: (ScreenAttribute?)t.GetCustomAttributes(typeof(ScreenAttribute), false).FirstOrDefault()))
            .Where(x => x.Screen is not null);

        foreach (var (type, screen) in pages)
        {
            var spec = specs[screen!.Id];
            var allowed = new HashSet<string?> { spec["view"]?.GetValue<string>() };
            if (spec["actions"] is JsonObject actions)
            {
                allowed.UnionWith(actions.Select(a => a.Value!.GetValue<string>()));
            }

            foreach (var attr in type.GetCustomAttributes(typeof(HasPermissionAttribute), true).Cast<HasPermissionAttribute>())
            {
                Assert.True(allowed.Contains(attr.Permission), $"{type.Name} requires '{attr.Permission}' which {screen.Id} does not declare");
            }
        }
    }
}
