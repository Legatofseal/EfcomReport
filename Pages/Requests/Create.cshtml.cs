using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Requests;

public class CreateModel(AppDbContext db, CurrentUserService currentUser, WorkCalendarService calendar, SubmissionService submissions) : PageModel
{
    [BindProperty] public int LeaveTypeId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today;
    [BindProperty, DataType(DataType.Date)] public DateTime EndDate { get; set; } = DateTime.Today;
    [BindProperty] public string? Notes { get; set; }
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    public List<LeaveType> LeaveTypes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        LeaveTypes = await db.LeaveTypes.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        if (LeaveTypeId == 0) LeaveTypeId = LeaveTypes.FirstOrDefault()?.Id ?? 0;
        if (Month is >= 1 and <= 12 && Year is >= 2020 and <= 2100)
            StartDate = EndDate = new DateTime(Year.Value, Month.Value, 1);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LeaveTypes = await db.LeaveTypes.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        var user = await currentUser.GetAsync(User);
        if (user?.EmployeeId is not int employeeId) return Forbid();
        StartDate = StartDate.Date; EndDate = EndDate.Date;
        if (EndDate < StartDate) ModelState.AddModelError(nameof(EndDate), "End date must not be earlier than start date.");
        if (!await db.LeaveTypes.AnyAsync(x => x.Id == LeaveTypeId && x.IsActive)) ModelState.AddModelError(nameof(LeaveTypeId), "Select an active leave type.");
        if (await db.AbsenceRequests.AnyAsync(x => x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= EndDate && x.EndDate >= StartDate)) ModelState.AddModelError(string.Empty, "This period overlaps another active absence. Edit or cancel the existing request first.");
        if (!ModelState.IsValid) return Page();
        db.AbsenceRequests.Add(new AbsenceRequest { EmployeeId = employeeId, LeaveTypeId = LeaveTypeId, StartDate = StartDate, EndDate = EndDate, Notes = Notes?.Trim(), CreatedByEmail = user.Email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await submissions.MarkRangeAsync(employeeId, StartDate, EndDate);
        TempData["Message"] = $"Absence saved. Counted workdays: {await calendar.CountAsync(StartDate, EndDate)}.";
        return RedirectToPage("/Index", new { month = StartDate.Month, year = StartDate.Year });
    }
}
