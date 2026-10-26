using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Auth;
using DentalLabOS.Contracts.Organization;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DentalLabOS.IntegrationTests;

[Collection(SqlCollection.Name)]
public sealed class FoundationTests(SqlServerFixture fx)
{
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [SqlFact]
    public async Task Health_reports_database_connectivity()
    {
        var health = await fx.Client().GetFromJsonAsync<HealthResponse>("/api/v1/health");
        Assert.True(health!.Database);
    }

    [SqlFact]
    public async Task Anonymous_requests_are_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.Client().GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.Client().GetAsync("/api/v1/branches")).StatusCode);
    }

    [SqlFact]
    public async Task Wrong_password_gets_generic_401_and_is_audited()
    {
        var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/token", new TokenRequest(fx.LabA.OwnerEmail, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var unknown = await fx.Client().PostAsJsonAsync("/api/v1/auth/token", new TokenRequest("nobody@nowhere.test", "x"));
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var failures = await fx.QueryAsync(db => db.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == fx.LabA.TenantId && a.Action == AuditActions.LoginFailed));
        Assert.True(failures >= 1);
    }

    [SqlFact]
    public async Task Me_returns_own_lab_and_permissions_resolved_on_server()
    {
        var me = await fx.Client(fx.LabA.Token).GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal(fx.LabA.TenantId, me!.TenantId);
        Assert.False(me.IsPlatformStaff);
        Assert.Contains("admin.branches.manage", me.Permissions);
        Assert.DoesNotContain(me.Permissions, p => p.StartsWith("platform.", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Tenant_isolation_lists_only_own_branches_and_hides_other_labs_records()
    {
        var a = await fx.Client(fx.LabA.Token).GetFromJsonAsync<PagedResult<BranchDto>>("/api/v1/branches");
        var b = await fx.Client(fx.LabB.Token).GetFromJsonAsync<PagedResult<BranchDto>>("/api/v1/branches");
        var branchA = Assert.Single(a!.Items);
        var branchB = Assert.Single(b!.Items);
        Assert.NotEqual(branchA.Id, branchB.Id);

        // Lab B cannot read or change Lab A's branch, and gets 404 (not 403) so existence is not revealed.
        var read = await fx.Client(fx.LabB.Token).GetAsync($"/api/v1/branches/{branchA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        var write = await fx.Client(fx.LabB.Token).PutAsJsonAsync($"/api/v1/branches/{branchA.Id}",
            new BranchUpsertRequest("HACK", "hacked", null, null, true, branchA.RowVersion));
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);

        var stillOriginal = await fx.Client(fx.LabA.Token).GetFromJsonAsync<BranchDto>($"/api/v1/branches/{branchA.Id}");
        Assert.Equal(branchA.Name, stillOriginal!.Name);
    }

    [SqlFact]
    public async Task Lab_users_cannot_call_platform_endpoints_and_platform_staff_cannot_read_lab_data()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await fx.Client(fx.LabA.Token).GetAsync("/api/v1/platform/tenants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fx.Client(fx.PlatformToken).GetAsync("/api/v1/branches")).StatusCode);
    }

    [SqlFact]
    public async Task Plan_branch_limit_is_enforced_on_the_server()
    {
        var response = await fx.Client(fx.LabA.Token).PostAsJsonAsync("/api/v1/branches", new BranchUpsertRequest("B2", "Second", null, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation.PlanLimitBranches", json.GetProperty("errors").GetProperty("Code")[0].GetProperty("key").GetString());
    }

    [SqlFact]
    public async Task Stale_rowversion_returns_409()
    {
        var client = fx.Client(fx.LabA.Token);
        var branch = (await client.GetFromJsonAsync<PagedResult<BranchDto>>("/api/v1/branches"))!.Items.Single();

        var first = await client.PutAsJsonAsync($"/api/v1/branches/{branch.Id}",
            new BranchUpsertRequest(branch.Code, branch.Name, "Address 1", null, true, branch.RowVersion));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await client.PutAsJsonAsync($"/api/v1/branches/{branch.Id}",
            new BranchUpsertRequest(branch.Code, branch.Name, "Address 2", null, true, branch.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, await CodeOf(stale));
    }

    [SqlFact]
    public async Task Validation_errors_are_localized_by_accept_language()
    {
        var client = fx.Client(fx.LabA.Token);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        var response = await client.PostAsJsonAsync("/api/v1/branches", new BranchUpsertRequest("bad code", "", null, null));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Please review the highlighted fields.", json.GetProperty("title").GetString());
    }

    [SqlFact]
    public async Task Numbering_is_gap_free_and_unique_under_concurrency()
    {
        const int count = 25;
        var date = new DateOnly(2026, 10, 26);
        var numbers = await Task.WhenAll(Enumerable.Range(0, count).Select(async _ =>
        {
            using var scope = fx.Api.Services.CreateScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginScope(fx.LabB.TenantId);
            return await scope.ServiceProvider.GetRequiredService<INumberingService>().NextAsync("WorkOrder", null, date);
        }));

        Assert.Equal(count, numbers.Distinct().Count());
        var sequence = numbers.Select(n => int.Parse(n[(n.LastIndexOf('-') + 1)..])).Order().ToArray();
        Assert.Equal(Enumerable.Range(sequence[0], count), sequence);
        Assert.All(numbers, n => Assert.StartsWith("WO-26-", n));
    }

    [SqlFact]
    public async Task Suspending_a_lab_revokes_existing_tokens_immediately_and_blocks_new_logins()
    {
        // Use a dedicated lab so other tests are unaffected.
        var platform = fx.Client(fx.PlatformToken);
        var labToken = fx.LabB.Token;
        Assert.Equal(HttpStatusCode.OK, (await fx.Client(labToken).GetAsync("/api/v1/me")).StatusCode);

        var tenant = await platform.GetFromJsonAsync<TenantDetailsDto>($"/api/v1/platform/tenants/{fx.LabB.TenantId}");
        var noReason = await platform.PostAsJsonAsync($"/api/v1/platform/tenants/{fx.LabB.TenantId}/status",
            new ChangeTenantStatusRequest(false, null, tenant!.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var suspend = await platform.PostAsJsonAsync($"/api/v1/platform/tenants/{fx.LabB.TenantId}/status",
            new ChangeTenantStatusRequest(false, "Unpaid invoice", tenant.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

        try
        {
            var revoked = await fx.Client(labToken).GetAsync("/api/v1/me");
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            Assert.Equal("tenant_suspended", await CodeOf(revoked));

            var login = await fx.Client().PostAsJsonAsync("/api/v1/auth/token", new TokenRequest(fx.LabB.OwnerEmail, SqlServerFixture.Password));
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

            var audited = await fx.QueryAsync(db => db.AuditLogs.IgnoreQueryFilters()
                .AnyAsync(a => a.Action == AuditActions.StatusChange && a.EntityId == fx.LabB.TenantId && a.Reason == "Unpaid invoice"));
            Assert.True(audited);
        }
        finally
        {
            var current = await platform.GetFromJsonAsync<TenantDetailsDto>($"/api/v1/platform/tenants/{fx.LabB.TenantId}");
            var reactivate = await platform.PostAsJsonAsync($"/api/v1/platform/tenants/{fx.LabB.TenantId}/status",
                new ChangeTenantStatusRequest(true, "Paid", current!.RowVersion));
            Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await fx.Client(labToken).GetAsync("/api/v1/me")).StatusCode);
    }

    [SqlFact]
    public async Task Audit_rows_are_scoped_to_their_lab()
    {
        var counts = await fx.QueryAsync(async db => await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.TenantId == fx.LabA.TenantId || a.TenantId == fx.LabB.TenantId)
            .GroupBy(a => a.TenantId).Select(g => g.Count()).ToListAsync());
        Assert.Equal(2, counts.Count);
        Assert.All(counts, c => Assert.True(c > 0));
    }
}
