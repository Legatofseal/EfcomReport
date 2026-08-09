using System.Security.Claims;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Services;

public static class PortalHostExtensions
{
    public static IServiceCollection AddPortalDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=efcom.db"));
        services.AddScoped<CurrentUserService>();
        services.AddScoped<WorkCalendarService>();
        services.AddScoped<SubmissionService>();
        services.AddScoped<ReportService>();
        services.AddScoped<EmailService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<InvoiceExtractionService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<AttachmentService>();
        services.AddSingleton<UiText>();
        services.AddHttpContextAccessor();
        return services;
    }

    public static IServiceCollection AddPortalAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("EfcomPortal");
        var keyDirectory = configuration["DataProtection:KeyDirectory"];
        if (!string.IsNullOrWhiteSpace(keyDirectory))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];
        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }).AddCookie(options =>
        {
            options.Cookie.Name = "Efcom.Auth";
            options.Cookie.Path = "/";
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/access-denied";
        });

        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.SaveTokens = false;
                options.Events.OnCreatingTicket = async context =>
                {
                    var principal = context.Principal;
                    var email = principal?.FindFirstValue(ClaimTypes.Email) ?? principal?.FindFirstValue("email");
                    if (string.IsNullOrWhiteSpace(email) || principal?.Identity is not ClaimsIdentity identity) return;
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    var adminEmails = (config["Authentication:AdminEmails"] ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var employee = await db.Employees.SingleOrDefaultAsync(x => x.Email == email);
                    var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
                    if (user is null)
                    {
                        user = new AppUser
                        {
                            Email = email,
                            DisplayName = principal.Identity?.Name ?? email,
                            EmployeeId = employee?.Id,
                            Role = adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase) ? "Admin" : "User"
                        };
                        db.AppUsers.Add(user);
                    }
                    else
                    {
                        user.LastSeenAtUtc = DateTime.UtcNow;
                        user.EmployeeId = employee?.Id;
                        if (adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase)) user.Role = "Admin";
                    }
                    await db.SaveChangesAsync();
                    identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
                };
            });
        }

        services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));
        return services;
    }

    public static void MapPortalAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/account/login", (IConfiguration config, string? returnUrl) =>
        {
            var clientId = config["Authentication:Google:ClientId"];
            var secret = config["Authentication:Google:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
                return config.GetValue<bool>("Authentication:EnableDevLogin")
                    ? Results.Redirect("/account/dev-login")
                    : Results.Content("Set Google OAuth credentials first.", "text/plain");
            var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                ? returnUrl : "/";
            return Results.Challenge(new AuthenticationProperties { RedirectUri = safeReturnUrl }, [GoogleDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        app.MapGet("/account/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        }).AllowAnonymous();

        app.MapGet("/account/access-denied", () => Results.Content("Access denied", "text/plain")).AllowAnonymous();

        app.MapGet("/account/dev-login", async (HttpContext http, IConfiguration config, AppDbContext db, string? email, string? name) =>
        {
            if (!app.Environment.IsDevelopment() || !config.GetValue<bool>("Authentication:EnableDevLogin")) return Results.NotFound();
            email ??= "admin@example.com";
            name ??= email;
            var admins = (config["Authentication:AdminEmails"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var employee = await db.Employees.SingleOrDefaultAsync(x => x.Email == email);
            var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
            var role = user?.Role == "Admin" || admins.Contains(email, StringComparer.OrdinalIgnoreCase) ? "Admin" : "User";
            if (user is null)
                db.AppUsers.Add(new AppUser { Email = email, DisplayName = name, EmployeeId = employee?.Id, Role = role });
            else
            {
                user.DisplayName = name;
                user.EmployeeId = employee?.Id;
                user.Role = role;
                user.LastSeenAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role)
            };
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, "Development")));
            return Results.Redirect("/");
        }).AllowAnonymous();
    }
}
