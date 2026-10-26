using System.Text.RegularExpressions;
using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Security;

namespace DentalLabOS.UnitTests;

public sealed partial class PermissionCatalogTests
{
    [GeneratedRegex("^[a-z]+(\\.[a-z]+){1,2}$")]
    private static partial Regex KeyFormat();

    [Fact]
    public void Every_key_follows_module_entity_action_format()
    {
        var bad = Permissions.All.Where(k => !KeyFormat().IsMatch(k)).ToArray();
        Assert.Empty(bad);
    }

    [Fact]
    public void Keys_are_unique()
    {
        var duplicates = Permissions.All.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Tenant_keys_never_include_platform_keys()
    {
        Assert.DoesNotContain(Permissions.TenantKeys, k => k.StartsWith("platform.", StringComparison.Ordinal));
        Assert.All(Permissions.PlatformKeys, k => Assert.StartsWith("platform.", k));
    }

    [Fact]
    public void Constants_used_in_code_exist_in_the_catalog()
    {
        var constants = typeof(Permissions).GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);
        Assert.All(constants, c => Assert.Contains(c, Permissions.All));
    }

    [Fact]
    public void Support_staff_get_a_strict_subset_of_owner_permissions()
    {
        var owner = Permissions.ForPlatformRole(PlatformRole.Owner);
        var support = Permissions.ForPlatformRole(PlatformRole.Support);
        Assert.Subset(owner.ToHashSet(), support.ToHashSet());
        Assert.DoesNotContain(Permissions.Platform.TenantsManage, support);
        Assert.Empty(Permissions.ForPlatformRole(PlatformRole.None));
    }

    [Fact]
    public void ModuleOf_returns_first_segment()
    {
        Assert.Equal("inventory", Permissions.ModuleOf("inventory.issue.post"));
    }
}

public sealed class SystemRoleTests
{
    [Fact]
    public void There_are_thirteen_builtin_roles_with_unique_keys()
    {
        Assert.Equal(13, SystemRoles.All.Count);
        Assert.Equal(SystemRoles.All.Count, SystemRoles.All.Select(r => r.Key).Distinct().Count());
    }

    [Fact]
    public void Every_role_permission_exists_in_the_tenant_catalog()
    {
        foreach (var role in SystemRoles.All)
        {
            var unknown = role.Permissions.Except(Permissions.TenantKeys).ToArray();
            Assert.True(unknown.Length == 0, $"{role.Key}: {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void Owner_has_every_tenant_permission()
    {
        var owner = SystemRoles.All.Single(r => r.Key == SystemRoles.Owner);
        Assert.Equal(Permissions.TenantKeys.Count, owner.Permissions.Distinct().Count());
    }

    [Fact]
    public void Technician_cannot_see_costs_or_manage_roles()
    {
        var tech = SystemRoles.All.Single(r => r.Key == SystemRoles.Technician);
        Assert.DoesNotContain("workorders.cost.read", tech.Permissions);
        Assert.DoesNotContain("admin.roles.manage", tech.Permissions);
    }

    [Fact]
    public void Manager_cannot_edit_roles()
    {
        var manager = SystemRoles.All.Single(r => r.Key == SystemRoles.Manager);
        Assert.DoesNotContain("admin.roles.manage", manager.Permissions);
    }
}
