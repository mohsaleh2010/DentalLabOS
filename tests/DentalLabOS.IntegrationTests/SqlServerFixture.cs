using System.Net.Http.Headers;
using System.Net.Http.Json;
using DentalLabOS.Application.Admin;
using DentalLabOS.Contracts;
using DentalLabOS.Contracts.Auth;
using DentalLabOS.Contracts.Platform;
using DentalLabOS.Infrastructure.Identity;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DentalLabOS.IntegrationTests;

/// <summary>
/// Runs only when <c>DLOS_TEST_SQL</c> points at a SQL Server (CI starts one as a service container).
/// Integration tests never fall back to an in-memory provider: tenant filters, row locks and rowversion
/// must be exercised against the real engine.
/// </summary>
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFixture.EnvVar)))
        {
            Skip = $"Set {SqlServerFixture.EnvVar} to a SQL Server connection string to run integration tests.";
        }
    }
}

/// <summary>A fresh database per test run with a platform owner and two labs (A and B), each with an owner.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string EnvVar = "DLOS_TEST_SQL";
    public const string Password = "Test#Passw0rd2026";
    private const string SetupToken = "integration-setup-token";

    private string _connectionString = string.Empty;

    public WebApplicationFactory<Program> Api { get; private set; } = default!;

    public LabContext LabA { get; private set; } = default!;

    public LabContext LabB { get; private set; } = default!;

    public string PlatformToken { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        Enabled = true;
        _connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = $"DentalLabOS_Test_{Guid.NewGuid():N}" }.ConnectionString;
        Api = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:DentalLabOS", _connectionString);
            b.UseSetting("Jwt:SigningKey", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));
            b.UseSetting("Bootstrap:SetupToken", SetupToken);
        });

        using (var scope = Api.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync(applyMigrations: true);
            await scope.ServiceProvider.GetRequiredService<PlatformBootstrapService>()
                .CreateOwnerAsync(SetupToken, "Platform Owner", "owner@platform.test", Password);
        }

        PlatformToken = await TokenAsync("owner@platform.test");
        LabA = await CreateLabAsync("lab-a");
        LabB = await CreateLabAsync("lab-b");
    }

    public async Task DisposeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        using (var scope = Api.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await Api.DisposeAsync();
    }

    public HttpClient Client(string? token = null)
    {
        var client = Api.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public async Task<string> TokenAsync(string email, string password = Password)
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/token", new TokenRequest(email, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }

    private async Task<LabContext> CreateLabAsync(string slug)
    {
        var platform = Client(PlatformToken);
        var plans = await platform.GetFromJsonAsync<List<PlanDto>>("/api/v1/platform/plans");
        var starter = plans!.First(p => p.MaxBranches == 1); // Starter: 1 branch, used by the plan-limit test
        var email = $"owner@{slug}.test";
        var created = await platform.PostAsJsonAsync("/api/v1/platform/tenants",
            new CreateTenantRequest($"Lab {slug}", slug, $"Owner {slug}", email, starter.Id, 14));
        created.EnsureSuccessStatusCode();
        var result = (await created.Content.ReadFromJsonAsync<CreateTenantResult>())!;

        using (var scope = Api.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<InvitationService>().AcceptAsync(result.OwnerInviteToken, Password);
        }

        var token = await TokenAsync(email);
        return new LabContext(result.TenantId, email, token);
    }

    /// <summary>Runs code against the database with all tenant filters removed (test assertions only).</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Api.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed record LabContext(Guid TenantId, string OwnerEmail, string Token);

[CollectionDefinition(Name)]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql";
}
