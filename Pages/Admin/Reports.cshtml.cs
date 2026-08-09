using System.Text;
using System.IO.Compression;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class ReportsModel(AppDbContext db, ReportService reports, EmailService email, AttachmentService attachments, ReminderService reminders) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? StartMonth { get; set; }
    [BindProperty(SupportsGet = true)] public int? StartYear { get; set; }
    [BindProperty(SupportsGet = true)] public int? EndMonth { get; set; }
    [BindProperty(SupportsGet = true)] public int? EndYear { get; set; }
    [BindProperty(SupportsGet = true)] public List<int>? EmployeeIds { get; set; }
    [BindProperty] public List<int> SelectedRecipientIds { get; set; } = [];

    public int CurrentStartMonth => StartMonth ?? LastCompletedMonth.Month;
    public int CurrentStartYear => StartYear ?? LastCompletedMonth.Year;
    public int CurrentEndMonth => EndMonth ?? LastCompletedMonth.Month;
    public int CurrentEndYear => EndYear ?? LastCompletedMonth.Year;
    public ReportView? Report { get; private set; }
    public List<Employee> Employees { get; private set; } = [];
    public List<int> SelectedEmployeeIds { get; private set; } = [];
    public List<ReportRecipient> Recipients { get; private set; } = [];
    public string CsvUrl => BuildReportUrl(
        new DateTime(CurrentStartYear, CurrentStartMonth, 1),
        new DateTime(CurrentEndYear, CurrentEndMonth, 1),
        SelectedEmployeeIds,
        "Csv");

    private static DateTime LastCompletedMonth => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (!TryGetPeriod(out var start, out var end)) return;
        SelectedEmployeeIds = NormalizeEmployeeSelection(EmployeeIds);
        Report = await reports.BuildRangeAsync(start.Year, start.Month, end.Year, end.Month, SelectedEmployeeIds);
    }

    public async Task<IActionResult> OnGetCsvAsync(int startYear, int startMonth, int endYear, int endMonth, List<int>? employeeIds)
    {
        if (!TryGetPeriod(startYear, startMonth, endYear, endMonth, out var start, out var end)) return BadRequest("Invalid report period.");
        var report = await reports.BuildRangeAsync(start.Year, start.Month, end.Year, end.Month, employeeIds);
        var package = await BuildReportPackageAsync(report, start, end, employeeIds);
        return File(package, "application/zip", ZipFileName(report));
    }

    public async Task<IActionResult> OnPostRemindAsync(
        int employeeId,
        int year,
        int month,
        int startYear,
        int startMonth,
        int endYear,
        int endMonth,
        List<int>? employeeIds)
    {
        try
        {
            var sent = await reminders.SendIndividualAsync(employeeId, year, month);
            TempData["Message"] = sent ? "Reminder sent." : "This monthly report is already confirmed.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Reminder was not sent: {ex.Message}";
        }

        if (!TryGetPeriod(startYear, startMonth, endYear, endMonth, out var start, out var end))
            return RedirectToPage();
        return Redirect(BuildReportUrl(start, end, employeeIds ?? [], ""));
    }

    public async Task<IActionResult> OnPostSendAsync(int startYear, int startMonth, int endYear, int endMonth, List<int>? employeeIds)
    {
        await LoadAsync();
        if (!TryGetPeriod(startYear, startMonth, endYear, endMonth, out var start, out var end))
        {
            ModelState.AddModelError(string.Empty, "Select a valid report period.");
            return Page();
        }

        SelectedEmployeeIds = NormalizeEmployeeSelection(employeeIds);
        Report = await reports.BuildRangeAsync(start.Year, start.Month, end.Year, end.Month, SelectedEmployeeIds);
        var targets = await db.ReportRecipients
            .Where(x => SelectedRecipientIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Email)
            .ToListAsync();
        if (targets.Count == 0) ModelState.AddModelError(string.Empty, "Select at least one recipient.");
        if (!ModelState.IsValid) return Page();

        try
        {
            await email.SendAsync(
                targets,
                $"Employee leave report {Report.PeriodLabel}",
                $"Employee leave report for {Report.PeriodLabel}.",
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(Report))).ToArray(),
                CsvFileName(Report));
            TempData["Message"] = "Report sent.";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        return Redirect(BuildReportUrl(start, end, SelectedEmployeeIds));
    }

    private async Task LoadAsync()
    {
        Employees = await db.Employees.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        Recipients = await db.ReportRecipients.Where(x => x.IsActive).OrderBy(x => x.Email).ToListAsync();
    }

    private List<int> NormalizeEmployeeSelection(IEnumerable<int>? employeeIds)
    {
        var activeIds = Employees.Select(x => x.Id).ToHashSet();
        var selected = employeeIds?.Where(activeIds.Contains).Distinct().ToList() ?? [];
        return selected.Count > 0 ? selected : activeIds.OrderBy(x => x).ToList();
    }

    private bool TryGetPeriod(out DateTime start, out DateTime end) =>
        TryGetPeriod(CurrentStartYear, CurrentStartMonth, CurrentEndYear, CurrentEndMonth, out start, out end);

    private bool TryGetPeriod(int startYear, int startMonth, int endYear, int endMonth, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (startYear is < 2020 or > 2100 || endYear is < 2020 or > 2100 || startMonth is < 1 or > 12 || endMonth is < 1 or > 12)
            return false;
        start = new DateTime(startYear, startMonth, 1);
        end = new DateTime(endYear, endMonth, 1).AddMonths(1).AddDays(-1);
        return start <= end;
    }

    private string BuildReportUrl(DateTime start, DateTime end, IEnumerable<int> employeeIds, string? handler = null)
    {
        var query = new List<string>
        {
            $"startYear={start.Year}", $"startMonth={start.Month}",
            $"endYear={end.Year}", $"endMonth={end.Month}"
        };
        if (!string.IsNullOrWhiteSpace(handler)) query.Insert(0, $"handler={handler}");
        query.AddRange(employeeIds.Select(id => $"employeeIds={id}"));
        return $"{Request.Path}?{string.Join("&", query)}";
    }

    private static string CsvFileName(ReportView report) =>
        report.IsSingleMonth
            ? $"leave-report-{report.StartYear}-{report.StartMonth:00}.csv"
            : $"leave-report-{report.StartYear}-{report.StartMonth:00}-to-{report.EndYear}-{report.EndMonth:00}.csv";

    private static string ZipFileName(ReportView report) =>
        report.IsSingleMonth
            ? $"leave-report-{report.StartYear}-{report.StartMonth:00}.zip"
            : $"leave-report-{report.StartYear}-{report.StartMonth:00}-to-{report.EndYear}-{report.EndMonth:00}.zip";

    private async Task<byte[]> BuildReportPackageAsync(ReportView report, DateTime start, DateTime end, IEnumerable<int>? employeeIds)
    {
        var selectedEmployeeIds = employeeIds?.Where(x => x > 0).Distinct().ToHashSet();
        var query = db.AbsenceRequests.AsNoTracking()
            .Where(x => !x.IsCancelled && x.StartDate <= end && x.EndDate >= start && x.AttachmentStorageName != null);
        if (selectedEmployeeIds is { Count: > 0 }) query = query.Where(x => selectedEmployeeIds.Contains(x.EmployeeId));
        var requests = await query.OrderBy(x => x.EmployeeId).ThenBy(x => x.StartDate).ToListAsync();

        await using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var csvEntry = archive.CreateEntry(CsvFileName(report), CompressionLevel.Fastest);
            await using (var csvStream = csvEntry.Open())
            {
                var csvBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(report))).ToArray();
                await csvStream.WriteAsync(csvBytes);
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in requests)
            {
                var path = attachments.GetPath(request.AttachmentStorageName);
                if (path is null || !System.IO.File.Exists(path)) continue;
                var originalName = Path.GetFileName(request.AttachmentOriginalName ?? $"absence-{request.Id}");
                var fileName = MakeUniqueFileName(originalName, usedNames);
                var documentEntry = archive.CreateEntry($"documents/{fileName}", CompressionLevel.Fastest);
                await using var input = System.IO.File.OpenRead(path);
                await using var output = documentEntry.Open();
                await input.CopyToAsync(output);
            }
        }

        return package.ToArray();
    }

    private static string MakeUniqueFileName(string originalName, HashSet<string> usedNames)
    {
        if (usedNames.Add(originalName)) return originalName;
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName}-{index}{extension}";
            if (usedNames.Add(candidate)) return candidate;
        }
    }

    private static string ToCsv(ReportView report)
    {
        var columns = new List<string> { "Report Period", "Employee" };
        foreach (var type in report.Types) { columns.Add($"{type.Name} Days"); columns.Add($"{type.Name} Entries"); }
        columns.Add("Total Leave Days"); columns.Add("Submission Status");
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', columns.Select(Escape)));
        foreach (var row in report.Rows)
        {
            var values = new List<string> { report.PeriodLabel, row.EmployeeName };
            foreach (var type in report.Types)
            {
                values.Add(row.DaysByType.GetValueOrDefault(type.Name).ToString());
                values.Add(row.EntryCounts.GetValueOrDefault(type.Name).ToString());
            }
            values.Add(row.TotalDays.ToString());
            values.Add(row.SubmissionState);
            sb.AppendLine(string.Join(',', values.Select(Escape)));
        }
        return sb.ToString();
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
