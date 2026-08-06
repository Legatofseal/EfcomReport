using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class RequestsModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public int CurrentMonth => Month ?? DateTime.Today.Month;
    public int CurrentYear => Year ?? DateTime.Today.Year;
    public List<AbsenceRequest> Requests { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var start = new DateTime(CurrentYear, CurrentMonth, 1);
        var end = start.AddMonths(1).AddDays(-1);
        Requests = await db.AbsenceRequests.Include(x => x.Employee).Include(x => x.LeaveType)
            .Where(x => !x.IsCancelled && x.StartDate <= end && x.EndDate >= start)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Employee.Name).ToListAsync();
    }
}
