using EfcomReport.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPortalDataServices(builder.Configuration);
builder.Services.AddPortalAuthentication(builder.Configuration);
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Requests");
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
    options.Conventions.AuthorizeFolder("/Invoices");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/language/set", (HttpContext http, string? language, string? returnUrl) =>
{
    var normalized = UiText.Normalize(language);
    http.Response.Cookies.Append(UiText.LanguageCookieName, normalized, new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps
    });
    var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
        ? returnUrl : "/";
    return Results.LocalRedirect(target);
});

PortalDatabaseInitializer.EnsureCreated(app.Services);
app.MapPortalAccountEndpoints();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
