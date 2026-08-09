using System.Security.Claims;
using System.Data;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=efcom.db"));
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<WorkCalendarService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InvoiceExtractionService>();
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddSingleton<UiText>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Requests");
    options.Conventions.AuthorizeFolder("/Invoices");
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
});
builder.Services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
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
            var adminEmails = (config["Authentication:AdminEmails"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isAdmin = adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
            var employee = await db.Employees.SingleOrDefaultAsync(x => x.Email == email);
            var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
            if (user is null)
            {
                user = new AppUser { Email = email, DisplayName = principal.Identity?.Name ?? email, EmployeeId = employee?.Id, Role = isAdmin ? "Admin" : "User" };
                db.AppUsers.Add(user);
            }
            else
            {
                user.LastSeenAtUtc = DateTime.UtcNow;
                user.EmployeeId = employee?.Id;
                if (isAdmin) user.Role = "Admin";
            }
            await db.SaveChangesAsync();
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
        };
    });
}

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
    var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
    return Results.LocalRedirect(target);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // The pilot started with EnsureCreated rather than migrations. Keep existing
    // local SQLite databases usable when a new pilot table is added.
    if (db.Database.IsSqlite())
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ReportRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ReportRequests" PRIMARY KEY AUTOINCREMENT,
                "EmployeeId" INTEGER NOT NULL,
                "Year" INTEGER NOT NULL,
                "Month" INTEGER NOT NULL,
                "RequestedByEmail" TEXT NOT NULL,
                "SentToEmail" TEXT NOT NULL,
                "RequestedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_ReportRequests_Employees_EmployeeId"
                    FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_ReportRequests_EmployeeId_Year_Month"
            ON "ReportRequests" ("EmployeeId", "Year", "Month");
            CREATE TABLE IF NOT EXISTS "InvoiceEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceEntries" PRIMARY KEY AUTOINCREMENT,
                "SubmittedByEmail" TEXT NOT NULL,
                "RecipientEmail" TEXT NOT NULL,
                "Customer" TEXT NOT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "CurrencySymbol" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "PaymentType" TEXT NOT NULL,
                "Comments" TEXT NULL,
                "AttachmentOriginalName" TEXT NULL,
                "AttachmentStorageName" TEXT NULL,
                "AttachmentContentType" TEXT NULL,
                "AttachmentSize" INTEGER NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "EmailSentAtUtc" TEXT NULL,
                "EmailError" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_InvoiceEntries_CreatedAtUtc"
            ON "InvoiceEntries" ("CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_InvoiceEntries_InvoiceNumber"
            ON "InvoiceEntries" ("InvoiceNumber");
            """);
        EnsureSqliteColumn(db, "AttachmentOriginalName", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentStorageName", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentContentType", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentSize", "INTEGER NULL");
    }
}

static void EnsureSqliteColumn(AppDbContext db, string column, string definition)
{
    var connection = db.Database.GetDbConnection();
    var wasOpen = connection.State == ConnectionState.Open;
    if (!wasOpen) db.Database.OpenConnection();
    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('AbsenceRequests') WHERE name = '{column}'";
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        if (!exists)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"AbsenceRequests\" ADD COLUMN \"{column}\" {definition}";
            alter.ExecuteNonQuery();
        }
    }
    finally
    {
        if (!wasOpen) db.Database.CloseConnection();
    }
}

app.MapGet("/account/login", (IConfiguration config, string? returnUrl) =>
{
    var clientId = config["Authentication:Google:ClientId"];
    var secret = config["Authentication:Google:ClientSecret"];
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
        return config.GetValue<bool>("Authentication:EnableDevLogin")
            ? Results.Redirect("/account/dev-login")
            : Results.Content("Set Google OAuth credentials first.", "text/plain");
    return Results.Challenge(new AuthenticationProperties { RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl }, new[] { GoogleDefaults.AuthenticationScheme });
});

app.MapGet("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/account/access-denied", () => Results.Content("Access denied", "text/plain"));

app.MapGet("/account/dev-login", async (HttpContext http, IConfiguration config, AppDbContext db, string? email, string? name) =>
{
    if (!app.Environment.IsDevelopment() || !config.GetValue<bool>("Authentication:EnableDevLogin")) return Results.NotFound();
    email ??= "admin@example.com"; name ??= email;
    var admins = (config["Authentication:AdminEmails"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var employee = await db.Employees.SingleOrDefaultAsync(x => x.Email == email);
    var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
    var role = user?.Role == "Admin" || admins.Contains(email, StringComparer.OrdinalIgnoreCase) ? "Admin" : "User";
    if (user is null) db.AppUsers.Add(new AppUser { Email = email, DisplayName = name, EmployeeId = employee?.Id, Role = role });
    else { user.DisplayName = name; user.EmployeeId = employee?.Id; user.Role = role; user.LastSeenAtUtc = DateTime.UtcNow; }
    await db.SaveChangesAsync();
    var claims = new[] { new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role) };
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, "Development")));
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
