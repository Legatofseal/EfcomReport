using System.Security.Claims;
using System.Text;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace EfcomReport.Services;

public sealed class CurrentUserService(AppDbContext db)
{
    public string? Email(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name;

    public async Task<AppUser?> GetAsync(ClaimsPrincipal principal)
    {
        var email = Email(principal);
        if (string.IsNullOrWhiteSpace(email)) return null;
        var user = await db.AppUsers.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Email == email);
        if (user is null || !user.IsActive || (user.Role != "Admin" && user.Employee?.IsActive != true)) return null;
        return user;
    }
}

public sealed class WorkCalendarService(AppDbContext db)
{
    public static DateTime Date(DateTime value) => value.Date;

    public static CalendarSchedule DefaultSchedule(DateTime date) =>
        new(date.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday), false);

    public static bool DefaultIsWorking(DateTime date) => DefaultSchedule(date).IsWorking;

    public async Task<Dictionary<DateTime, CalendarSchedule>> OverridesAsync(DateTime start, DateTime end)
    {
        return await db.WorkdayOverrides
            .Where(x => x.Date >= start.Date && x.Date <= end.Date)
            .ToDictionaryAsync(x => x.Date.Date, x => new CalendarSchedule(x.IsWorking, x.IsWorking && x.IsHalfDay));
    }

    public async Task<bool> IsWorkingAsync(DateTime date)
    {
        var overrideDay = await db.WorkdayOverrides.SingleOrDefaultAsync(x => x.Date == date.Date);
        return overrideDay?.IsWorking ?? DefaultSchedule(date).IsWorking;
    }

    public async Task<decimal> CountAsync(DateTime start, DateTime end)
    {
        if (end.Date < start.Date) return 0;
        var overrides = await OverridesAsync(start, end);
        var count = 0m;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            var schedule = overrides.TryGetValue(day, out var value) ? value : DefaultSchedule(day);
            if (schedule.IsWorking) count += schedule.IsHalfDay ? 0.5m : 1m;
        }
        return count;
    }

    public async Task<List<CalendarDay>> MonthAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var overrides = await OverridesAsync(start, end);
        return Enumerable.Range(0, end.Day)
            .Select(offset => start.AddDays(offset))
            .Select(date =>
            {
                var schedule = overrides.TryGetValue(date.Date, out var value) ? value : DefaultSchedule(date);
                return new CalendarDay(date, schedule.IsWorking, schedule.IsHalfDay, overrides.ContainsKey(date.Date));
            })
            .ToList();
    }
}

public sealed record CalendarSchedule(bool IsWorking, bool IsHalfDay);
public sealed record CalendarDay(DateTime Date, bool IsWorking, bool IsHalfDay, bool IsOverride)
{
    public string Status => !IsOverride ? "default" : IsHalfDay ? "half" : IsWorking ? "working" : "off";
}

public sealed record StoredAttachment(string OriginalName, string StorageName, string ContentType, long Size);

public sealed class AttachmentService(IConfiguration configuration)
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    public string RootPath
    {
        get
        {
            var configured = configuration["FileStorage:RootPath"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            return OperatingSystem.IsLinux()
                ? "/home/efcomreport-uploads"
                : Path.Combine(AppContext.BaseDirectory, "uploads");
        }
    }

    public string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Choose a non-empty document.";
        if (file.Length > MaxFileSize) return "The document must be 10 MB or smaller.";
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension)) return "Allowed document types: PDF, JPG and PNG.";
        return null;
    }

    public async Task<StoredAttachment> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        return await SaveToAsync(file, RootPath, cancellationToken);
    }

    public async Task<StoredAttachment> SaveInvoiceAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        return await SaveToAsync(file, InvoiceRootPath, cancellationToken);
    }

    public string InvoiceRootPath => Path.Combine(RootPath, "invoices");

    private async Task<StoredAttachment> SaveToAsync(IFormFile file, string targetRoot, CancellationToken cancellationToken)
    {
        var validationError = Validate(file);
        if (validationError is not null) throw new InvalidOperationException(validationError);
        Directory.CreateDirectory(targetRoot);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storageName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(targetRoot, storageName);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        return new StoredAttachment(Path.GetFileName(file.FileName), storageName,
            contentType, file.Length);
    }

    public string? GetPath(string? storageName)
    {
        return GetPathInRoot(storageName, RootPath);
    }

    public string? GetInvoicePath(string? storageName)
    {
        return GetPathInRoot(storageName, InvoiceRootPath);
    }

    private static string? GetPathInRoot(string? storageName, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(storageName) || storageName != Path.GetFileName(storageName)) return null;
        var path = Path.GetFullPath(Path.Combine(rootPath, storageName));
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    public void Delete(string? storageName)
    {
        var path = GetPath(storageName);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    public void DeleteInvoice(string? storageName)
    {
        var path = GetInvoicePath(storageName);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    public string GetDownloadName(AbsenceRequest request)
    {
        var extension = Path.GetExtension(request.AttachmentOriginalName ?? "document").ToLowerInvariant();
        var uploader = SafeFilePart(request.AttachmentUploadedByName ?? request.AttachmentUploadedByEmail ?? request.CreatedByEmail, "user");
        var uploadedAt = (request.AttachmentUploadedAtUtc ?? request.CreatedAtUtc).ToLocalTime().ToString("yyyyMMdd-HHmm");
        var period = $"{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}";
        return $"{uploader}_{uploadedAt}_{period}{extension}";
    }

    private static string SafeFilePart(string value, string fallback)
    {
        var cleaned = string.Concat(value.Trim().Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }
}

public sealed class SubmissionService(AppDbContext db)
{
    public async Task MarkAsync(int employeeId, int year, int month, bool hasAbsence)
    {
        var row = await db.MonthlySubmissions.SingleOrDefaultAsync(x =>
            x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (row is null)
        {
            row = new MonthlySubmission { EmployeeId = employeeId, Year = year, Month = month };
            db.MonthlySubmissions.Add(row);
        }
        row.HasAbsence = hasAbsence;
        row.SubmittedAtUtc = DateTime.UtcNow;
        row.IsConfirmed = false;
        row.ConfirmedAtUtc = null;
        await db.SaveChangesAsync();
    }

    public async Task ConfirmAsync(int employeeId, int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var row = await db.MonthlySubmissions.SingleOrDefaultAsync(x =>
            x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (row is null)
        {
            row = new MonthlySubmission { EmployeeId = employeeId, Year = year, Month = month };
            db.MonthlySubmissions.Add(row);
        }

        row.HasAbsence = await db.AbsenceRequests.AnyAsync(x =>
            x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= end && x.EndDate >= start);
        row.SubmittedAtUtc = DateTime.UtcNow;
        row.IsConfirmed = true;
        row.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkRangeAsync(int employeeId, DateTime start, DateTime end)
    {
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        while (cursor <= last)
        {
            await MarkAsync(employeeId, cursor.Year, cursor.Month, true);
            cursor = cursor.AddMonths(1);
        }
    }

    public async Task RefreshRangeAsync(int employeeId, DateTime start, DateTime end)
    {
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        while (cursor <= last)
        {
            var monthEnd = cursor.AddMonths(1).AddDays(-1);
            var row = await db.MonthlySubmissions.SingleOrDefaultAsync(x =>
                x.EmployeeId == employeeId && x.Year == cursor.Year && x.Month == cursor.Month);
            if (row is null)
            {
                row = new MonthlySubmission { EmployeeId = employeeId, Year = cursor.Year, Month = cursor.Month };
                db.MonthlySubmissions.Add(row);
            }

            row.HasAbsence = await db.AbsenceRequests.AnyAsync(x =>
                x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= monthEnd && x.EndDate >= cursor);
            row.SubmittedAtUtc = DateTime.UtcNow;
            row.IsConfirmed = false;
            row.ConfirmedAtUtc = null;
            cursor = cursor.AddMonths(1);
        }
        await db.SaveChangesAsync();
    }
}

public sealed class ReportService(AppDbContext db, WorkCalendarService calendar)
{
    public Task<ReportView> BuildAsync(int year, int month, IReadOnlyCollection<int>? employeeIds = null) =>
        BuildRangeAsync(year, month, year, month, employeeIds);

    public async Task<ReportView> BuildRangeAsync(int startYear, int startMonth, int endYear, int endMonth, IReadOnlyCollection<int>? employeeIds = null)
    {
        var rangeStart = new DateTime(startYear, startMonth, 1);
        var rangeEnd = new DateTime(endYear, endMonth, 1).AddMonths(1).AddDays(-1);
        var periods = new List<ReportPeriod>();
        for (var cursor = rangeStart; cursor <= rangeEnd; cursor = cursor.AddMonths(1))
            periods.Add(new ReportPeriod(cursor.Year, cursor.Month));

        var overrides = await calendar.OverridesAsync(rangeStart, rangeEnd);
        var types = await db.LeaveTypes.OrderBy(x => x.Id).ToListAsync();
        var requestedEmployeeIds = employeeIds?.Where(x => x > 0).Distinct().ToHashSet();
        var employeeQuery = db.Employees.Where(x => x.IsActive);
        if (requestedEmployeeIds is { Count: > 0 }) employeeQuery = employeeQuery.Where(x => requestedEmployeeIds.Contains(x.Id));
        var employees = await employeeQuery.OrderBy(x => x.Name).ToListAsync();
        var reportEmployeeIds = employees.Select(x => x.Id).ToHashSet();
        var submissions = await db.MonthlySubmissions
            .Where(x => x.Year > startYear || (x.Year == startYear && x.Month >= startMonth))
            .Where(x => x.Year < endYear || (x.Year == endYear && x.Month <= endMonth))
            .Where(x => reportEmployeeIds.Contains(x.EmployeeId))
            .ToListAsync();
        var submissionLookup = submissions.ToDictionary(x => (x.EmployeeId, x.Year, x.Month));
        var requests = await db.AbsenceRequests
            .Include(x => x.LeaveType)
            .Include(x => x.Employee)
            .Where(x => !x.IsCancelled && reportEmployeeIds.Contains(x.EmployeeId) && x.StartDate <= rangeEnd && x.EndDate >= rangeStart)
            .ToListAsync();

        var rows = employees.ToDictionary(x => x.Id, x => new ReportRow(x.Id, x.Name));
        foreach (var request in requests)
        {
            if (!rows.TryGetValue(request.EmployeeId, out var row)) continue;
            row.EntryCounts[request.LeaveType.Name] = row.EntryCounts.GetValueOrDefault(request.LeaveType.Name) + 1;
            var start = request.StartDate.Date < rangeStart ? rangeStart : request.StartDate.Date;
            var end = request.EndDate.Date > rangeEnd ? rangeEnd : request.EndDate.Date;
            var typeDays = row.DayFractionsByType.GetValueOrDefault(request.LeaveType.Name);
            if (typeDays is null)
            {
                typeDays = new Dictionary<DateTime, decimal>();
                row.DayFractionsByType[request.LeaveType.Name] = typeDays;
            }
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var schedule = overrides.TryGetValue(day, out var value)
                    ? value
                    : WorkCalendarService.DefaultSchedule(day);
                if (!schedule.IsWorking) continue;
                var availableFraction = schedule.IsHalfDay ? 0.5m : 1m;
                var absenceFraction = request.IsHalfDay ? 0.5m : 1m;
                var leaveFraction = Math.Min(availableFraction, absenceFraction);
                typeDays[day.Date] = Math.Max(typeDays.GetValueOrDefault(day.Date), leaveFraction);
                row.DayFractions[day.Date] = Math.Max(row.DayFractions.GetValueOrDefault(day.Date), leaveFraction);
            }
            row.DaysByType[request.LeaveType.Name] = typeDays.Values.Sum();
        }

        foreach (var rowPair in rows)
        {
            var row = rowPair.Value;
            row.TotalDays = row.DayFractions.Values.Sum();
            foreach (var period in periods)
            {
                var hasSubmission = submissionLookup.TryGetValue((rowPair.Key, period.Year, period.Month), out var submission);
                row.SubmissionStatuses.Add(new ReportSubmissionStatus(
                    period.Year,
                    period.Month,
                    hasSubmission && submission!.IsConfirmed ? "Confirmed" : "Not confirmed",
                    hasSubmission ? submission!.ConfirmedAtUtc : null));
            }
        }

        return new ReportView(startYear, startMonth, endYear, endMonth, periods, types, rows.Values.ToList());
    }
}

public sealed record ReportPeriod(int Year, int Month);

public sealed record ReportSubmissionStatus(int Year, int Month, string State, DateTime? ConfirmedAtUtc);

public sealed class ReportView(int startYear, int startMonth, int endYear, int endMonth, IReadOnlyList<ReportPeriod> periods, IReadOnlyList<LeaveType> types, IReadOnlyList<ReportRow> rows)
{
    public int StartYear { get; } = startYear;
    public int StartMonth { get; } = startMonth;
    public int EndYear { get; } = endYear;
    public int EndMonth { get; } = endMonth;
    public int Year => StartYear;
    public int Month => StartMonth;
    public IReadOnlyList<ReportPeriod> Periods { get; } = periods;
    public IReadOnlyList<LeaveType> Types { get; } = types;
    public IReadOnlyList<ReportRow> Rows { get; } = rows;
    public bool IsSingleMonth => StartYear == EndYear && StartMonth == EndMonth;
    public string PeriodLabel => IsSingleMonth
        ? $"{StartYear}-{StartMonth:00}"
        : $"{StartYear}-{StartMonth:00} to {EndYear}-{EndMonth:00}";
}

public sealed class ReportRow(int employeeId, string employeeName)
{
    public int EmployeeId { get; } = employeeId;
    public string EmployeeName { get; } = employeeName;
    public Dictionary<string, decimal> DaysByType { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> EntryCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<DateTime, decimal>> DayFractionsByType { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<DateTime, decimal> DayFractions { get; } = [];
    public decimal TotalDays { get; set; }
    public List<ReportSubmissionStatus> SubmissionStatuses { get; } = [];
    public string SubmissionState => string.Join("; ", SubmissionStatuses.Select(x => $"{x.Year}-{x.Month:00}: {x.State}"));
}

public sealed class EmailService(IConfiguration configuration, ILogger<EmailService> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
        && !string.IsNullOrWhiteSpace(configuration["Email:From"]);

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string body, byte[]? attachment = null, string? attachmentName = null)
    {
        await SendDocumentAsync(recipients, subject, body, attachment, attachmentName, "text/csv");
    }

    public async Task SendDocumentAsync(IEnumerable<string> recipients, string subject, string body,
        byte[]? attachment = null, string? attachmentName = null, string? attachmentContentType = null)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email is not configured. Set Email:SmtpHost and Email:From.");

        using var message = new MailMessage { From = new MailAddress(from), Subject = subject, Body = body };
        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            message.To.Add(recipient);
        if (attachment is not null && !string.IsNullOrWhiteSpace(attachmentName))
            message.Attachments.Add(new Attachment(new MemoryStream(attachment), attachmentName, attachmentContentType ?? "application/octet-stream"));

        using var client = new SmtpClient(host, int.TryParse(configuration["Email:SmtpPort"], out var port) ? port : 587)
        {
            EnableSsl = !string.Equals(configuration["Email:DisableSsl"], "true", StringComparison.OrdinalIgnoreCase)
        };
        var username = configuration["Email:Username"];
        var password = configuration["Email:Password"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);
        await client.SendMailAsync(message);
        logger.LogInformation("Sent report email to {Count} recipients", message.To.Count);
    }
}

public sealed class ReminderService(AppDbContext db, EmailService email, IConfiguration configuration, ILogger<ReminderService> logger)
{
    public async Task<int> RunAsync(bool force)
    {
        var now = DateTime.Now;
        var dueDay = configuration.GetValue("Reminder:DayOfMonth", 1);
        if (!force && now.Day != dueDay) return 0;
        if (!force && await db.ReminderRuns.AnyAsync(x => x.Year == now.Year && x.Month == now.Month)) return 0;

        var employees = await db.Employees.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        var submissions = await db.MonthlySubmissions
            .Where(x => x.Year == now.Year && x.Month == now.Month)
            .ToDictionaryAsync(x => x.EmployeeId);
        var missing = employees.Where(x => !submissions.TryGetValue(x.Id, out var submission) || !submission.IsConfirmed).ToList();
        if (missing.Count == 0)
        {
            if (!force) { db.ReminderRuns.Add(new ReminderRun { Year = now.Year, Month = now.Month }); await db.SaveChangesAsync(); }
            return 0;
        }
        if (!email.IsConfigured) throw new InvalidOperationException("Email is not configured for reminders.");
        foreach (var employee in missing)
            await SendReminderAsync(employee, now.Year, now.Month);
        db.ReminderRuns.Add(new ReminderRun { Year = now.Year, Month = now.Month });
        await db.SaveChangesAsync();
        logger.LogInformation("Sent {Count} monthly reminders", missing.Count);
        return missing.Count;
    }

    public async Task<bool> SendIndividualAsync(int employeeId, int year, int month)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12)
            throw new InvalidOperationException("Select a valid month and year.");

        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.IsActive);
        if (employee is null) throw new InvalidOperationException("The employee is not active.");
        var submission = await db.MonthlySubmissions.SingleOrDefaultAsync(x =>
            x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (submission?.IsConfirmed == true) return false;

        await SendReminderAsync(employee, year, month);
        return true;
    }

    private async Task SendReminderAsync(Employee employee, int year, int month)
    {
        if (!email.IsConfigured) throw new InvalidOperationException("Email is not configured for reminders.");
        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        var link = $"{publicUrl}/?month={month}&year={year}";
        await email.SendAsync(
            [employee.Email],
            "Monthly leave report confirmation reminder",
            $"Hello {employee.Name},\n\nPlease fill in your absence information for {year}-{month:00} and confirm the monthly report.\n\nOpen the leave tracker: {link}");
    }
}

public sealed class ReminderWorker(IServiceScopeFactory scopes, ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReminderService>().RunAsync(false);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Monthly reminder check failed"); }
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}
