using System.Text;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class ReportsModel(AppDbContext db, ReportService reports, EmailService email) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    [BindProperty] public List<int> SelectedRecipientIds { get; set; } = [];
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public ReportView? Report { get; private set; }
    public List<ReportRecipient> Recipients { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Report = await reports.BuildAsync(CurrentYear, CurrentMonth);
        Recipients = await db.ReportRecipients.Where(x => x.IsActive).OrderBy(x => x.Email).ToListAsync();
    }

    public async Task<IActionResult> OnGetCsvAsync(int year, int month)
    {
        var report = await reports.BuildAsync(year, month); var csv = ToCsv(report);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", $"leave-report-{year}-{month:00}.csv");
    }

    public async Task<IActionResult> OnPostSendAsync(int year, int month)
    {
        Report = await reports.BuildAsync(year, month); Recipients = await db.ReportRecipients.Where(x => x.IsActive).OrderBy(x => x.Email).ToListAsync();
        var targets = await db.ReportRecipients.Where(x => SelectedRecipientIds.Contains(x.Id) && x.IsActive).Select(x => x.Email).ToListAsync();
        if (targets.Count == 0) ModelState.AddModelError(string.Empty, "Select at least one recipient.");
        if (!ModelState.IsValid) return Page();
        try
        {
            await email.SendAsync(targets, $"Employee leave report {year}-{month:00}", $"Employee leave report for {year}-{month:00}.", Encoding.UTF8.GetBytes(ToCsv(Report)), $"leave-report-{year}-{month:00}.csv");
            TempData["Message"] = "Report sent.";
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return Page(); }
        return RedirectToPage(new { year, month });
    }

    private static string ToCsv(ReportView report)
    {
        var columns = new List<string> { "Employee" };
        foreach (var type in report.Types) { columns.Add($"{type.Name} Days"); columns.Add($"{type.Name} Entries"); }
        columns.Add("Total Leave Days"); columns.Add("Monthly Submission");
        var sb = new StringBuilder(); sb.AppendLine(string.Join(',', columns.Select(Escape)));
        foreach (var row in report.Rows)
        {
            var values = new List<string> { row.EmployeeName };
            foreach (var type in report.Types) { values.Add(row.DaysByType.GetValueOrDefault(type.Name).ToString()); values.Add(row.EntryCounts.GetValueOrDefault(type.Name).ToString()); }
            values.Add(row.TotalDays.ToString()); values.Add(row.SubmissionState); sb.AppendLine(string.Join(',', values.Select(Escape)));
        }
        return sb.ToString();
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
