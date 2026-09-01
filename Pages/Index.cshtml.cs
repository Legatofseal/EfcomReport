using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages;

[Authorize]
public class IndexModel(AppDbContext db, CurrentUserService currentUser, SubmissionService submissions, WorkCalendarService calendar) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public AppUser? UserRecord { get; private set; }
    public List<AbsenceRequest> Requests { get; private set; } = [];
    public Dictionary<int, decimal> RequestDays { get; private set; } = [];
    public string SubmissionState { get; private set; } = "Not confirmed";
    public DateTime? ConfirmedAtUtc { get; private set; }
    public int CurrentMonth => Month ?? DefaultReportPeriod.Month;
    public int CurrentYear => Year ?? DefaultReportPeriod.Year;
    public bool IsCurrentPeriodBeforeConfirmationDate =>
        SelectedPeriod == CurrentPeriod && DateTime.Today.Day < 25;
    public bool CanConfirm => SelectedPeriod <= CurrentPeriod && !IsCurrentPeriodBeforeConfirmationDate;

    public async Task OnGetAsync()
    {
        UserRecord = await currentUser.GetAsync(User);
        if (UserRecord?.EmployeeId is not int employeeId) return;
        var start = new DateTime(CurrentYear, CurrentMonth, 1);
        var end = start.AddMonths(1).AddDays(-1);
        Requests = await db.AbsenceRequests.Include(x => x.LeaveType)
            .Where(x => x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= end && x.EndDate >= start)
            .OrderBy(x => x.StartDate).ToListAsync();
        var calendarDays = await calendar.MonthAsync(CurrentYear, CurrentMonth);
        var schedules = calendarDays.ToDictionary(
            x => x.Date.Date,
            x => new CalendarSchedule(x.IsWorking, x.IsHalfDay));
        foreach (var request in Requests)
        {
            var requestStart = request.StartDate.Date < start ? start : request.StartDate.Date;
            var requestEnd = request.EndDate.Date > end ? end : request.EndDate.Date;
            RequestDays[request.Id] = WorkCalendarService.CountAbsence(requestStart, requestEnd, request.IsHalfDay, schedules);
        }
        var submission = await db.MonthlySubmissions.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == CurrentYear && x.Month == CurrentMonth);
        SubmissionState = submission?.IsConfirmed == true ? "Confirmed" : "Not confirmed";
        ConfirmedAtUtc = submission?.ConfirmedAtUtc;
    }

    public async Task<IActionResult> OnPostConfirmAsync(int year, int month)
    {
        var user = await currentUser.GetAsync(User);
        if (user?.EmployeeId is not int employeeId) return Forbid();
        if (year is < 2020 or > 2100 || month is < 1 or > 12) return BadRequest();
        var selectedPeriod = new DateTime(year, month, 1);
        if (selectedPeriod > CurrentPeriod) return BadRequest("Future reports cannot be confirmed.");
        if (selectedPeriod == CurrentPeriod && DateTime.Today.Day < 25)
        {
            TempData["Message"] = "The current month can be confirmed from the 25th.";
            return RedirectToPage(new { month, year });
        }
        await submissions.ConfirmAsync(employeeId, year, month);
        TempData["Message"] = "Monthly report confirmed.";
        return RedirectToPage(new { month, year });
    }

    private static DateTime CurrentPeriod => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private static DateTime DefaultReportPeriod =>
        CurrentPeriod.AddMonths(DateTime.Today.Day < 25 ? -1 : 0);

    private DateTime SelectedPeriod => new(CurrentYear, CurrentMonth, 1);
}
