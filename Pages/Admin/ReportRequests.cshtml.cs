using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class ReportRequestsModel(AppDbContext db, EmailService email, CurrentUserService currentUser, IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    [BindProperty(SupportsGet = true)] public List<int>? EmployeeIds { get; set; }
    [BindProperty] public List<int> SelectedEmployeeIds { get; set; } = [];
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public List<EmployeeSubmissionStatus> EmployeeStatuses { get; private set; } = [];
    public List<ReportRequest> RecentRequests { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
        SelectedEmployeeIds = EmployeeIds is { Count: > 0 }
            ? NormalizeSelection(EmployeeIds)
            : EmployeeStatuses.Where(x => x.State == "Not confirmed").Select(x => x.Employee.Id).ToList();
    }

    public async Task<IActionResult> OnPostSendAsync(int year, int month)
    {
        Month = month;
        Year = year;
        await LoadAsync();

        if (month is < 1 or > 12 || year is < 2020 or > 2100)
            ModelState.AddModelError(string.Empty, "Select a valid month and year.");

        SelectedEmployeeIds = NormalizeSelection(SelectedEmployeeIds);
        if (SelectedEmployeeIds.Count == 0) ModelState.AddModelError(string.Empty, "Select at least one employee.");
        if (!ModelState.IsValid) return Page();

        var employees = await db.Employees
            .Where(x => x.IsActive && SelectedEmployeeIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync();
        var admin = await currentUser.GetAsync(User);
        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        var link = $"{publicUrl}/?month={month}&year={year}";
        var subject = $"Action required: confirm leave report for {year}-{month:00}";
        var sentCount = 0;
        var errors = new List<string>();

        foreach (var employee in employees)
        {
            var body = $"Hello {employee.Name},\n\nPlease open the leave tracker, fill in your absence information for {year}-{month:00}, and confirm the monthly report.\n\nOpen the tracker: {link}\n\nThis request was sent by {admin?.Email ?? "an administrator"}.";
            try
            {
                await email.SendAsync([employee.Email], subject, body);
                db.ReportRequests.Add(new ReportRequest
                {
                    EmployeeId = employee.Id,
                    Year = year,
                    Month = month,
                    RequestedByEmail = admin?.Email ?? "unknown",
                    SentToEmail = employee.Email,
                    RequestedAtUtc = DateTime.UtcNow
                });
                sentCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{employee.Email}: {ex.Message}");
            }
        }

        if (sentCount > 0) await db.SaveChangesAsync();
        if (sentCount > 0) TempData["Message"] = $"Report requests sent to {sentCount} employee(s).";
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
        if (errors.Count > 0) return Page();
        return RedirectToPage(new { year, month });
    }

    private async Task LoadAsync()
    {
        var employees = await db.Employees.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        var submissions = await db.MonthlySubmissions
            .Where(x => x.Year == CurrentYear && x.Month == CurrentMonth)
            .ToDictionaryAsync(x => x.EmployeeId);
        EmployeeStatuses = employees.Select(employee => new EmployeeSubmissionStatus(
            employee,
            submissions.TryGetValue(employee.Id, out var submission)
                ? (submission.IsConfirmed ? "Confirmed" : "Not confirmed")
                : "Not confirmed")).ToList();
        RecentRequests = await db.ReportRequests
            .Include(x => x.Employee)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(25)
            .ToListAsync();
    }

    private List<int> NormalizeSelection(IEnumerable<int>? employeeIds)
    {
        var activeIds = EmployeeStatuses.Select(x => x.Employee.Id).ToHashSet();
        return employeeIds?.Where(activeIds.Contains).Distinct().ToList() ?? [];
    }
}

public sealed record EmployeeSubmissionStatus(Employee Employee, string State);
