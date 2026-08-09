using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages;

[Authorize]
public class IndexModel(AppDbContext db, CurrentUserService currentUser, SubmissionService submissions) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public AppUser? UserRecord { get; private set; }
    public List<AbsenceRequest> Requests { get; private set; } = [];
    public string SubmissionState { get; private set; } = "Not confirmed";
    public DateTime? ConfirmedAtUtc { get; private set; }
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
        SubmissionState = submission?.IsConfirmed == true ? "Confirmed" : "Not confirmed";
        ConfirmedAtUtc = submission?.ConfirmedAtUtc;
    }

    public async Task<IActionResult> OnPostConfirmAsync(int year, int month)
    {
        var user = await currentUser.GetAsync(User);
        if (user?.EmployeeId is not int employeeId) return Forbid();
        if (year is < 2020 or > 2100 || month is < 1 or > 12) return BadRequest();
        await submissions.ConfirmAsync(employeeId, year, month);
        TempData["Message"] = "Monthly report confirmed.";
        return RedirectToPage(new { month, year });
    }
}
