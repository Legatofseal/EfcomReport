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

    public static bool DefaultIsWorking(DateTime date) =>
        date.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday);

    public async Task<Dictionary<DateTime, bool>> OverridesAsync(DateTime start, DateTime end)
    {
        return await db.WorkdayOverrides
            .Where(x => x.Date >= start.Date && x.Date <= end.Date)
            .ToDictionaryAsync(x => x.Date.Date, x => x.IsWorking);
    }

    public async Task<bool> IsWorkingAsync(DateTime date)
    {
        var overrideDay = await db.WorkdayOverrides.SingleOrDefaultAsync(x => x.Date == date.Date);
        return overrideDay?.IsWorking ?? DefaultIsWorking(date);
    }

    public async Task<int> CountAsync(DateTime start, DateTime end)
    {
        if (end.Date < start.Date) return 0;
        var overrides = await OverridesAsync(start, end);
        var count = 0;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            var working = overrides.TryGetValue(day, out var value) ? value : DefaultIsWorking(day);
            if (working) count++;
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
            .Select(date => new CalendarDay(date,
                overrides.TryGetValue(date.Date, out var value) ? value : DefaultIsWorking(date),
                overrides.ContainsKey(date.Date)))
            .ToList();
    }
}

public sealed record CalendarDay(DateTime Date, bool IsWorking, bool IsOverride);

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
        var validationError = Validate(file);
        if (validationError is not null) throw new InvalidOperationException(validationError);
        Directory.CreateDirectory(RootPath);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storageName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(RootPath, storageName);
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
        if (string.IsNullOrWhiteSpace(storageName) || storageName != Path.GetFileName(storageName)) return null;
        var path = Path.GetFullPath(Path.Combine(RootPath, storageName));
        var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    public void Delete(string? storageName)
    {
        var path = GetPath(storageName);
        if (path is not null && File.Exists(path)) File.Delete(path);
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

        var rows = employees.ToDictionary(x => x.Id, x => new ReportRow(x.Name));
        foreach (var request in requests)
        {
            if (!rows.TryGetValue(request.EmployeeId, out var row)) continue;
            row.EntryCounts[request.LeaveType.Name] = row.EntryCounts.GetValueOrDefault(request.LeaveType.Name) + 1;
            var start = request.StartDate.Date < rangeStart ? rangeStart : request.StartDate.Date;
            var end = request.EndDate.Date > rangeEnd ? rangeEnd : request.EndDate.Date;
            var typeDays = row.DaySets.GetValueOrDefault(request.LeaveType.Name);
            if (typeDays is null)
            {
                typeDays = new HashSet<DateTime>();
                row.DaySets[request.LeaveType.Name] = typeDays;
            }
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var working = overrides.TryGetValue(day, out var value)
                    ? value
                    : WorkCalendarService.DefaultIsWorking(day);
                if (working) typeDays.Add(day.Date);
            }
            row.DaysByType[request.LeaveType.Name] = typeDays.Count;
        }

        foreach (var rowPair in rows)
        {
            var row = rowPair.Value;
            row.TotalDays = row.DaySets.Values.SelectMany(x => x).Distinct().Count();
            foreach (var period in periods)
            {
                row.SubmissionStatuses.Add(new ReportSubmissionStatus(
                    period.Year,
                    period.Month,
                    submissionLookup.TryGetValue((rowPair.Key, period.Year, period.Month), out var submission)
                        ? (submission.HasAbsence ? "Absences submitted" : "No absence")
                        : "Did not submit this month"));
            }
        }

        return new ReportView(startYear, startMonth, endYear, endMonth, periods, types, rows.Values.ToList());
    }
}

public sealed record ReportPeriod(int Year, int Month);

public sealed record ReportSubmissionStatus(int Year, int Month, string State);

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

public sealed class ReportRow(string employeeName)
{
    public string EmployeeName { get; } = employeeName;
    public Dictionary<string, int> DaysByType { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> EntryCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashSet<DateTime>> DaySets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int TotalDays { get; set; }
    public List<ReportSubmissionStatus> SubmissionStatuses { get; } = [];
    public string SubmissionState => string.Join("; ", SubmissionStatuses.Select(x => $"{x.Year}-{x.Month:00}: {x.State}"));
}

public sealed class EmailService(IConfiguration configuration, ILogger<EmailService> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
        && !string.IsNullOrWhiteSpace(configuration["Email:From"]);

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string body, byte[]? attachment = null, string? attachmentName = null)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email is not configured. Set Email:SmtpHost and Email:From.");

        using var message = new MailMessage { From = new MailAddress(from), Subject = subject, Body = body };
        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            message.To.Add(recipient);
        if (attachment is not null && !string.IsNullOrWhiteSpace(attachmentName))
            message.Attachments.Add(new Attachment(new MemoryStream(attachment), attachmentName, "text/csv"));

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
        var submitted = await db.MonthlySubmissions.Where(x => x.Year == now.Year && x.Month == now.Month).Select(x => x.EmployeeId).ToListAsync();
        var missing = employees.Where(x => !submitted.Contains(x.Id)).ToList();
        if (missing.Count == 0)
        {
            if (!force) { db.ReminderRuns.Add(new ReminderRun { Year = now.Year, Month = now.Month }); await db.SaveChangesAsync(); }
            return 0;
        }
        if (!email.IsConfigured) throw new InvalidOperationException("Email is not configured for reminders.");
        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        var link = $"{publicUrl}/?month={now.Month}&year={now.Year}";
        foreach (var employee in missing)
            await email.SendAsync([employee.Email], "Monthly leave submission reminder", $"Hello {employee.Name},\n\nPlease submit your absence information for {now:MMMM yyyy}, or confirm that there was no absence.\n\nOpen the leave tracker: {link}");
        db.ReminderRuns.Add(new ReminderRun { Year = now.Year, Month = now.Month });
        await db.SaveChangesAsync();
        logger.LogInformation("Sent {Count} monthly reminders", missing.Count);
        return missing.Count;
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
