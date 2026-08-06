using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages;

[Authorize]
public class IndexModel(AppDbContext db, CurrentUserService currentUser) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public AppUser? UserRecord { get; private set; }
    public List<AbsenceRequest> Requests { get; private set; } = [];
    public string SubmissionState { get; private set; } = "Missing";
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;

    public async Task OnGetAsync()
    {
        UserRecord = await currentUser.GetAsync(User);
        if (UserRecord?.EmployeeId is not int employeeId) return;
        var start = new DateTime(CurrentYear, CurrentMonth, 1);
        var end = start.AddMonths(1).AddDays(-1);
        Requests = await db.AbsenceRequests.Include(x => x.LeaveType)
            .Where(x => x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= end && x.EndDate >= start)
            .OrderBy(x => x.StartDate).ToListAsync();
        var submission = await db.MonthlySubmissions.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == CurrentYear && x.Month == CurrentMonth);
        SubmissionState = submission is null ? "Missing" : submission.HasAbsence ? "Absences submitted" : "No absence";
    }

    public async Task<IActionResult> OnPostNoAbsenceAsync(int year, int month)
    {
        var user = await currentUser.GetAsync(User);
        if (user?.EmployeeId is not int employeeId) return Forbid();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        if (await db.AbsenceRequests.AnyAsync(x => x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= end && x.EndDate >= start))
        {
            TempData["Message"] = "You already have an absence in this month. Edit or cancel it instead of submitting No absence.";
            return RedirectToPage(new { month, year });
        }
        var submission = await db.MonthlySubmissions.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (submission is null)
        {
            submission = new MonthlySubmission { EmployeeId = employeeId, Year = year, Month = month };
            db.MonthlySubmissions.Add(submission);
        }
        submission.HasAbsence = false;
        submission.SubmittedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { month, year });
    }
}
