# EfcomReport local pilot

Small monolithic ASP.NET Core Razor Pages application for employee leave tracking.

## Included in this pilot

- Google OAuth configuration with a development-only login for local testing.
- User access limited to the linked employee record.
- Admin access for employees, leave types, report recipients, work-calendar overrides and reports.
- Monthly submission state: absence submitted, no absence, or missing.
- SQLite through Entity Framework Core.
- Overlap protection for active absence requests.
- Monthly report calculation based on the selected month only. A request crossing month boundaries is clipped to the report period.
- Dynamic report columns for additional leave types.
- CSV download and optional SMTP report delivery.
- Monthly reminder worker plus a manual local test button.

## Local run

```powershell
dotnet restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

Open `http://localhost:5186/account/dev-login` for the local admin. The development login is disabled outside Development and must not be exposed publicly.

The local database is `efcom.db`. It is intentionally created empty except for the three initial leave types: Vacation, Miluim and Sick Leave.

## Google login

Create a Google OAuth web client and set:

```powershell
$env:Authentication__Google__ClientId = "..."
$env:Authentication__Google__ClientSecret = "..."
$env:Authentication__AdminEmails = "admin@example.com"
```

The callback path is `/signin-google`. Add the correct local and Azure callback URLs to the Google OAuth client. The first administrator is configured through `Authentication:AdminEmails`; employees are then entered in the admin page by their Google email.

## Email

Report delivery and reminders require SMTP settings:

```powershell
$env:Email__SmtpHost = "smtp.example.com"
$env:Email__SmtpPort = "587"
$env:Email__Username = "..."
$env:Email__Password = "..."
$env:Email__From = "leave-tracker@example.com"
```

The reminder worker checks the configured day of month (`Reminder:DayOfMonth`, default `1`). For Azure, a scheduled Azure job is preferable to relying on a background worker in a scaled-out web container.

## Docker

```powershell
docker build -t efcom-report .
docker run --rm -p 8080:8080 -v efcom-report-data:/data `
  -e ConnectionStrings__DefaultConnection="Data Source=/data/efcom.db" `
  efcom-report
```

For the pilot, use one container instance and persistent storage. Before accounting-critical use, move the EF Core provider from SQLite to PostgreSQL/Azure SQL and keep the same application model.
