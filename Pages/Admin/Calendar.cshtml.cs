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

    public async Task<IActionResult> OnPostSetAsync(DateTime date, int year, int month, string status)
    {
        status = (status ?? "default").Trim().ToLowerInvariant();
        if (status is not ("default" or "working" or "half" or "off"))
            return BadRequest("Invalid calendar status.");

        var email = currentUser.Email(User) ?? "unknown";
        var row = await db.WorkdayOverrides.SingleOrDefaultAsync(x => x.Date == date.Date);
        if (status == "default")
        {
            if (row is not null) db.WorkdayOverrides.Remove(row);
        }
        else
        {
            row ??= new Models.WorkdayOverride { Date = date.Date };
            row.IsWorking = status != "off";
            row.IsHalfDay = status == "half";
            row.UpdatedByEmail = email;
            row.UpdatedAtUtc = DateTime.UtcNow;
            if (row.Id == 0) db.WorkdayOverrides.Add(row);
        }
        await db.SaveChangesAsync(); return RedirectToPage(new { year, month });
    }
}
