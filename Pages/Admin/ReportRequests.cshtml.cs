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
    [BindProperty] public int EmployeeId { get; set; }
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public List<Employee> Employees { get; private set; } = [];
    public List<ReportRequest> RecentRequests { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostSendAsync(int year, int month)
    {
        await LoadAsync();
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == EmployeeId && x.IsActive);
        if (employee is null) ModelState.AddModelError(nameof(EmployeeId), "Select an active employee.");
        if (month is < 1 or > 12 || year is < 2020 or > 2100) ModelState.AddModelError(string.Empty, "Select a valid month and year.");
        if (!ModelState.IsValid) return Page();

        var admin = await currentUser.GetAsync(User);
        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        var link = $"{publicUrl}/?month={month}&year={year}";
        var subject = $"Action required: leave submission for {year}-{month:00}";
        var body = $"Hello {employee!.Name},\n\nPlease open the leave tracker and submit your absence information for {year}-{month:00}. If you had no absence, choose 'No absence this month'.\n\nOpen the tracker: {link}\n\nThis request was sent by {admin?.Email ?? "an administrator"}.";
        try
        {
            await email.SendAsync([employee.Email], subject, body);
            db.ReportRequests.Add(new ReportRequest { EmployeeId = employee.Id, Year = year, Month = month, RequestedByEmail = admin?.Email ?? "unknown", SentToEmail = employee.Email, RequestedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            TempData["Message"] = $"Report request sent to {employee.Email}.";
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return Page(); }
        return RedirectToPage(new { year, month });
    }

    private async Task LoadAsync()
    {
        Employees = await db.Employees.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        RecentRequests = await db.ReportRequests.Include(x => x.Employee).OrderByDescending(x => x.RequestedAtUtc).Take(25).ToListAsync();
    }
}
