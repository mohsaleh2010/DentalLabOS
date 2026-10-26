using System.Reflection;

namespace DentalLabOS.ArchitectureTests;

public sealed class LayeringTests
{
    private static readonly Assembly Domain = typeof(Domain.Common.Entity).Assembly;
    private static readonly Assembly Application = typeof(Application.Common.AppException).Assembly;
    private static readonly Assembly Contracts = typeof(Contracts.ErrorCodes).Assembly;

    private static string[] References(Assembly assembly) => assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_has_no_framework_or_infrastructure_dependencies()
    {
        var refs = References(Domain);
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("DentalLabOS.", StringComparison.Ordinal));
    }

    [Fact]
    public void Contracts_depend_on_nothing_in_the_solution()
    {
        Assert.DoesNotContain(References(Contracts), r => r.StartsWith("DentalLabOS.", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_ui()
    {
        var refs = References(Application);
        Assert.DoesNotContain("DentalLabOS.Infrastructure", refs);
        Assert.DoesNotContain("DentalLabOS.Web", refs);
        Assert.DoesNotContain("DentalLabOS.Api", refs);
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_owned_entities_derive_from_TenantEntity()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Domain.Common.ITenantOwned).IsAssignableFrom(t))
            .Where(t => !typeof(Domain.Common.TenantEntity).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToArray();
        Assert.Empty(offenders);
    }
}

public sealed class SecurityRulesTests
{
    /// <summary>Bypassing tenant filters is allowed only where reviewed (plan: TENANT_ISOLATION.md).</summary>
    private static readonly HashSet<string> IgnoreQueryFiltersAllowList =
    [
        "src/DentalLabOS.Application/Admin/AuditQueryService.cs",     // platform audit: TenantId == null rows only
        "src/DentalLabOS.Application/Admin/InvitationService.cs",     // anonymous accept: lookup by token hash, then scoped
        "src/DentalLabOS.Application/Platform/PlatformTenantService.cs", // platform staff: subscription metadata only
    ];

    [Fact]
    public void IgnoreQueryFilters_is_only_used_in_reviewed_files()
    {
        var offenders = Repo.SourceFiles("*.cs")
            .Where(f => File.ReadAllText(f).Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            .Select(Repo.Relative)
            .Where(f => !IgnoreQueryFiltersAllowList.Contains(f))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void No_secrets_or_connection_strings_in_committed_settings()
    {
        string[] secretNames = ["SigningKey", "SetupToken", "Password", "ApiKey", "ClientSecret"];
        foreach (var file in Repo.SourceFiles("appsettings*.json"))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file),
                new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
            foreach (var (name, value) in Flatten(doc.RootElement, string.Empty))
            {
                var isSecret = name.StartsWith("ConnectionStrings:", StringComparison.Ordinal) || secretNames.Any(s => name.EndsWith(s, StringComparison.Ordinal));
                Assert.False(isSecret && !string.IsNullOrEmpty(value), $"{Repo.Relative(file)}: '{name}' must be empty in Git; use user-secrets or environment variables.");
            }
        }
    }

    private static IEnumerable<(string Name, string? Value)> Flatten(System.Text.Json.JsonElement e, string prefix)
    {
        if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                foreach (var x in Flatten(p.Value, prefix.Length == 0 ? p.Name : $"{prefix}:{p.Name}"))
                {
                    yield return x;
                }
            }
        }
        else
        {
            yield return (prefix, e.ValueKind == System.Text.Json.JsonValueKind.String ? e.GetString() : e.ToString());
        }
    }

    [Fact]
    public void Admin_platform_and_master_data_pages_require_a_permission()
    {
        string[] guarded = [".Pages.Admin", ".Pages.Platform", ".Pages.Customers", ".Pages.Suppliers", ".Pages.Items", ".Pages.PriceLists"];
        var pages = typeof(Web.Infrastructure.AppPageModel).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns && guarded.Any(g => ns.Contains(g, StringComparison.Ordinal)))
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel).IsAssignableFrom(t))
            .ToArray();

        Assert.NotEmpty(pages);
        var missing = pages.Where(t => !t.GetCustomAttributes(typeof(Web.Infrastructure.HasPermissionAttribute), true).Any()).Select(t => t.FullName).ToArray();
        Assert.Empty(missing);
    }

    [Fact]
    public void Views_have_no_inline_scripts_blocked_by_the_content_security_policy()
    {
        // script-src 'self': behaviour lives in dlos.js and is wired with data-* attributes.
        var offenders = Repo.SourceFiles("*.cshtml")
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"<script(?![^>]*\bsrc=)[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(Repo.Relative)
            .ToArray();
        Assert.Empty(offenders);
    }
}
