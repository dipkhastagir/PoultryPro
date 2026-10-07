using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using PoultryPro.Infrastructure;

// Fixed culture so decimals ("12.5"), dates and currency behave the same on every Windows locale.
var appCulture = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = appCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var builder = WebApplication.CreateBuilder(args);

// ── Database: start-up initialiser + shared status ──────────────────────────
builder.Services.AddSingleton<DbStatus>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IDbConnectionFactory>(sp =>
    new SqlConnectionFactory(sp.GetRequiredService<DbStatus>().ConnectionString));

// ── Application services: every class "XxxService" implementing "IXxxService" is registered ──
foreach (var impl in Assembly.GetExecutingAssembly().GetTypes()
             .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Service")))
{
    var contract = impl.GetInterfaces().FirstOrDefault(i => i.Name == "I" + impl.Name);
    if (contract != null) builder.Services.AddScoped(contract, impl);
}

// ── Authentication (cookie) + global "must be signed in" policy ─────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
        o.Cookie.Name = "PoultryPro.Auth";
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    o.Filters.AddService<AuditActionFilter>();
}).AddRazorRuntimeCompilation();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Create / upgrade the database before serving requests ──────────────────
await app.Services.GetRequiredService<DatabaseInitializer>().RunAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseMiddleware<DbReadyMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();   // attribute-routed APIs (IoT ingest, mobile sync, public trace)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
