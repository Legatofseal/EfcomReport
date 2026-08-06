using EfcomReport.Data;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class CalendarModel(AppDbContext db, WorkCalendarService calendar, CurrentUserService currentUser) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public List<CalendarDay> Days { get; private set; } = [];
    public async Task OnGetAsync() => Days = await calendar.MonthAsync(CurrentYear, CurrentMonth);

    public async Task<IActionResult> OnPostToggleAsync(DateTime date, int year, int month)
    {
        var email = currentUser.Email(User) ?? "unknown";
        var row = await db.WorkdayOverrides.SingleOrDefaultAsync(x => x.Date == date.Date);
        var defaultValue = WorkCalendarService.DefaultIsWorking(date);
        if (row is null) db.WorkdayOverrides.Add(new Models.WorkdayOverride { Date = date.Date, IsWorking = !defaultValue, UpdatedByEmail = email });
        else { row.IsWorking = !row.IsWorking; row.UpdatedByEmail = email; row.UpdatedAtUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync(); return RedirectToPage(new { year, month });
    }
}
