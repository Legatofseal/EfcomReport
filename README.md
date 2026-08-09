# EfcomReport local pilot

ASP.NET Core Razor Pages portal with separate tracker and invoice web applications.

## Application split

The repository now contains two independently buildable web applications plus a shared core:

- `EfcomReport` — the leave tracker and portal shell, served at `/`.
- `EfcomInvoices` — the invoice application, served at `/Invoices/...` on the same public domain.
- `EfcomCore` — shared EF models, database initialization, authentication, email, file storage and invoice extraction services.

Route `/Invoices/*` to the `EfcomInvoices` App Service through the domain reverse proxy; route the remaining paths to `EfcomReport`. Both applications must use the same `ConnectionStrings__DefaultConnection`, the same `DataProtection__KeyDirectory` storage and the same Google OAuth settings so the authentication cookie is shared. A local SQLite file and an app-local key directory are suitable for development only. For two Azure App Services, use a shared database and shared external key storage; the current SQLite and local file storage arrangement is not a safe cross-App-Service production topology.

The solution file is `EfcomPortal.slnx`. The `main_efcomreport.yml` workflow deploys the tracker. The manual `invoices_efcomreport.yml` workflow deploys the invoice app after the second App Service exists and the `AZUREAPPSERVICE_PUBLISHPROFILE_INVOICES` GitHub secret is configured. The same-domain reverse-proxy route must send `/Invoices/*` to that second App Service.

## Included in this pilot

- Google OAuth configuration with a development-only login for local testing.
- User access limited to the linked employee record.
- Admin access for employees, leave types, report recipients, work-calendar overrides and reports.
- Monthly submission state: absence submitted, no absence, or missing.
- SQLite through Entity Framework Core.
- Overlap protection for active absence requests.
- Report calculation clips requests to the selected month range and counts distinct working dates across that range. Submission status remains visible for each month in the range.
- Dynamic report columns for additional leave types.
- Reports default to all active employees for the last completed month; administrators can select employees and a start/end month range. CSV downloads and emailed reports use the same filters.
- CSV download and optional SMTP report delivery.
- Monthly reminder worker plus a manual local test button.
- Admin can send monthly submission requests to multiple selected employees by email; employees who have not submitted for the selected month are selected by default.
- Admins can invite another Google account as an administrator from **Employees and settings**. If SMTP is configured, the invitation includes the sign-in link.
- Sick Leave requests can optionally include a PDF, JPG or PNG document up to 10 MB; only the employee and administrators can download it.
- Invoice entries are a separate authenticated module. Users can record the recipient email, customer, invoice number, currency symbol, amount, payment type, comments and an optional PDF/JPG/PNG document up to 10 MB. The entry is stored in SQLite and the same information is sent by SMTP using the `EFCOM_INVOICE,...` subject format. Administrators can see all entries; regular users can see their own entries.
- Invoice entry forms can attempt to prefill customer, invoice number, currency, amount, payment/reference digits and item description from an uploaded document. The result is always shown for manual confirmation before the entry is saved or emailed. Text PDFs use `pdftotext`; scanned PDFs and images use Tesseract OCR when the tools and language packs are installed. The parser removes common RTL control characters, recognizes Hebrew labels and scores total/invoice candidates so receipt numbers, order numbers and intermediate amounts are not selected accidentally. Currency remains optional, so leaving it blank produces the original amount-only subject format.
- All authenticated users can view the read-only work calendar; only administrators can change calendar days.
- The interface supports English and Hebrew. Only interface text is translated; employee names, emails, leave types and other entered data remain unchanged.

## Local run

```powershell
dotnet restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

Open `http://localhost:5186/account/dev-login` for the local admin. The development login is disabled outside Development and must not be exposed publicly.

The local database is `efcom.db`. It is intentionally created empty except for the three initial leave types: Vacation, Miluim and Sick Leave. Local uploaded documents are stored under the application `uploads` directory unless `FileStorage:RootPath` is configured. Invoice documents are stored in its `invoices` subdirectory.

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

Set `App__PublicUrl` to the public app URL in Azure so targeted emails contain the correct link.

Invoice extraction uses these optional settings when executable paths are not available on `PATH`:

```text
InvoiceExtraction__PdfToTextPath=
InvoiceExtraction__PdfToPpmPath=
InvoiceExtraction__TesseractPath=
InvoiceExtraction__TesseractLanguages=eng+heb+rus
```

The Docker image installs Poppler and Tesseract with English, Hebrew and Russian language packs. A code-only App Service deployment must provide equivalent executables or will fall back to manual entry.

On Azure, long-term application data is stored in the SQLite file `/home/efcomreport.db`. Sick-leave documents are stored outside SQLite under `/home/efcomreport-uploads`. Both locations use App Service persistent storage when `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true`; the data is not stored in GitHub. This is persistent storage, not a backup, so configure App Service Backup and verify a restore before relying on it for accounting records. Set `FileStorage__RootPath` only if a different persistent path is required.

The reminder worker checks the configured day of month (`Reminder:DayOfMonth`, default `1`). For Azure, a scheduled Azure job is preferable to relying on a background worker in a scaled-out web container.

## Docker

```powershell
docker build -t efcom-report .
docker run --rm -p 8080:8080 -v efcom-report-data:/home `
  efcom-report
```

The container includes the application and creates the SQLite database on first start. The database is stored in `/home`, not in the image layer.

## One-resource Azure pilot

The smallest Azure setup is one Linux App Service with the required App Service Plan. If you choose **Publish: Code**, Azure can deploy the source from GitHub using GitHub Actions and no container registry is needed. The Dockerfile remains available for local testing and for a later container deployment.

If you choose **Publish: Container**, App Service needs a container registry. You can use GitHub Container Registry instead of creating an Azure Container Registry, but the registry is still required for the Docker image. In Deployment Center, select GitHub Actions and let Azure generate the build-and-deploy workflow from this repository's Dockerfile.

Set these App Service application settings:

```text
WEBSITES_PORT=8080
WEBSITES_ENABLE_APP_SERVICE_STORAGE=true
ConnectionStrings__DefaultConnection=Data Source=/home/efcomreport.db
App__PublicUrl=https://<your-app-name>.azurewebsites.net
Authentication__EnableDevLogin=false
Authentication__AdminEmails=alexeyp@efcom.co.il
```

Use one instance and do not enable autoscale. Turn on App Service backups and periodically verify that the application data can be restored. This SQLite arrangement is suitable for the pilot only; before accounting-critical use, move the EF Core provider to PostgreSQL/Azure SQL.
