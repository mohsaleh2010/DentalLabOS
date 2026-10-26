using DentalLabOS.Api.Endpoints;
using DentalLabOS.Api.Infrastructure;
using DentalLabOS.Contracts;
using DentalLabOS.Infrastructure;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Infrastructure.Security;
using DentalLabOS.SharedUI.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Bearer tokens only. The signing key comes from configuration/secrets (Development falls back to a random per-process key).
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = jwt.ResolveKey(builder.Environment.IsDevelopment());
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(signingKey);
builder.Services.AddSingleton<TokenIssuer>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
        };
    });
builder.Services.AddPermissionAuthorization();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddSingleton<IStringLocalizer>(_ => new JsonStringLocalizer());
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.SetDefaultCulture("ar-EG").AddSupportedCultures("ar-EG", "en-US").AddSupportedUICultures("ar-EG", "en-US");
    o.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddApiRateLimiting();
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "DentalLab OS API";
    doc.Info.Version = "v1";
    doc.Info.Description = "Tenant-scoped REST API. Authenticate with POST /api/v1/auth/token and send `Authorization: Bearer <token>`.";
    return Task.CompletedTask;
}));

var app = builder.Build();

// Localization runs first so error responses written by the exception handler use the request's language.
app.UseRequestLocalization();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
app.UseAuthentication();
app.UseMiddleware<AccessStateMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

var v1 = app.MapGroup("/api/v1");
v1.MapGet("/health", async (AppDbContext db, TimeProvider time, CancellationToken ct) =>
    {
        var database = await db.Database.CanConnectAsync(ct);
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        return TypedResults.Ok(new HealthResponse(database ? "Healthy" : "Degraded", version, time.GetUtcNow().UtcDateTime, database));
    })
    .AllowAnonymous().WithTags("Health").DisableRateLimiting();
v1.MapAuthEndpoints();
v1.MapOrganizationEndpoints();
v1.MapPlatformEndpoints();

await app.RunAsync();

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
