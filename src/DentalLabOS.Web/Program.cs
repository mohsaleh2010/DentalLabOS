using DentalLabOS.Infrastructure;
using DentalLabOS.Infrastructure.Persistence;
using DentalLabOS.Infrastructure.Security;
using DentalLabOS.SharedUI.Localization;
using DentalLabOS.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Authentication: Identity application cookie for the web app (the API uses bearer tokens).
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/login";
    o.LogoutPath = "/logout";
    o.AccessDeniedPath = "/error/403";
    o.Cookie.Name = "dlos.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddPermissionAuthorization();
builder.Services.AddAuthorization();

// Localization from shared JSON resources (Arabic default, English available).
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddSingleton<IStringLocalizer>(_ => new JsonStringLocalizer());
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture(UiPreferences.Cultures[0]);
    o.SupportedCultures = UiPreferences.Cultures;
    o.SupportedUICultures = UiPreferences.Cultures;
    o.RequestCultureProviders = [new CookieRequestCultureProvider(), new UiPreferences.ClaimCultureProvider()];
});

builder.Services.AddScoped<LayoutContext>();
builder.Services.AddMemoryCache();
builder.Services.AddRazorPages(o =>
    {
        o.Conventions.AuthorizeFolder("/");
        foreach (var page in new[] { "/Account/Login", "/Account/Setup", "/Account/AcceptInvite", "/Common/Error", "/Common/Offline", "/Common/SessionExpired", "/Common/Culture", "/Index" })
        {
            o.Conventions.AllowAnonymousToPage(page);
        }
    })
    .AddMvcOptions(o => o.Filters.Add<AppExceptionFilter>());
builder.Services.AddAntiforgery(o => o.Cookie.Name = "dlos.xsrf");
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/500");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");

// Baseline security headers (CSP allows only self-hosted assets; inline styles are used for progress widths).
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(self), geolocation=(), microphone=()";
    h.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AccessStateMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

await using (var scope = app.Services.CreateAsyncScope())
{
    var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment());
    await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync(applyMigrations);

    // Logs the one-time setup token if no platform owner exists yet.
    if (!await scope.ServiceProvider.GetRequiredService<DentalLabOS.Infrastructure.Identity.PlatformBootstrapService>().IsSetupOpenAsync())
    {
        _ = scope.ServiceProvider.GetRequiredService<DentalLabOS.Infrastructure.Identity.SetupTokenProvider>().Token;
    }
}

await app.RunAsync();
