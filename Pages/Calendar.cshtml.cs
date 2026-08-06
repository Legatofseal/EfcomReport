using EfcomReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EfcomReport.Pages;

[Authorize]
public class CalendarModel(WorkCalendarService calendar) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public List<CalendarDay> Days { get; private set; } = [];

    public async Task OnGetAsync() => Days = await calendar.MonthAsync(CurrentYear, CurrentMonth);
}
