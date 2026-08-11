using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Requests;

public class CreateModel(AppDbContext db, CurrentUserService currentUser, WorkCalendarService calendar, SubmissionService submissions, AttachmentService attachments) : PageModel
{
    [BindProperty] public int LeaveTypeId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today;
    [BindProperty, DataType(DataType.Date)] public DateTime EndDate { get; set; } = DateTime.Today;
    [BindProperty] public bool IsHalfDay { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public IFormFile? Attachment { get; set; }
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
        if (IsHalfDay && StartDate != EndDate) ModelState.AddModelError(nameof(IsHalfDay), "Half-day absence must use the same start and end date.");
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == LeaveTypeId && x.IsActive);
        if (leaveType is null) ModelState.AddModelError(nameof(LeaveTypeId), "Select an active leave type.");
        var isSickLeave = string.Equals(leaveType?.Name, "Sick Leave", StringComparison.OrdinalIgnoreCase);
        if (Attachment is not null && !isSickLeave) ModelState.AddModelError(nameof(Attachment), "A document can only be attached to Sick Leave.");
        if (Attachment is not null && isSickLeave)
        {
            var attachmentError = attachments.Validate(Attachment);
            if (attachmentError is not null) ModelState.AddModelError(nameof(Attachment), attachmentError);
        }
        if (await db.AbsenceRequests.AnyAsync(x => x.EmployeeId == employeeId && !x.IsCancelled && x.StartDate <= EndDate && x.EndDate >= StartDate)) ModelState.AddModelError(string.Empty, "This period overlaps another active absence. Edit or cancel the existing request first.");
        if (!ModelState.IsValid) return Page();
        StoredAttachment? storedAttachment = null;
        try
        {
            if (Attachment is not null) storedAttachment = await attachments.SaveAsync(Attachment);
            var uploadedAtUtc = Attachment is null ? (DateTime?)null : DateTime.UtcNow;
            db.AbsenceRequests.Add(new AbsenceRequest
            {
                EmployeeId = employeeId, LeaveTypeId = LeaveTypeId, StartDate = StartDate, EndDate = EndDate, IsHalfDay = IsHalfDay,
                Notes = Notes?.Trim(), CreatedByEmail = user.Email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                AttachmentOriginalName = storedAttachment?.OriginalName, AttachmentStorageName = storedAttachment?.StorageName,
                AttachmentContentType = storedAttachment?.ContentType, AttachmentSize = storedAttachment?.Size,
                AttachmentUploadedByName = storedAttachment is null ? null : user.DisplayName,
                AttachmentUploadedByEmail = storedAttachment is null ? null : user.Email,
                AttachmentUploadedAtUtc = uploadedAtUtc
            });
            await db.SaveChangesAsync();
        }
        catch
        {
            if (storedAttachment is not null) attachments.Delete(storedAttachment.StorageName);
            throw;
        }
        await submissions.MarkRangeAsync(employeeId, StartDate, EndDate);
        var countedWorkdays = await calendar.CountAbsenceAsync(StartDate, EndDate, IsHalfDay);
        TempData["Message"] = $"Absence saved. Counted workdays: {countedWorkdays:0.##}.";
        return RedirectToPage("/Index", new { month = StartDate.Month, year = StartDate.Year });
    }
}
