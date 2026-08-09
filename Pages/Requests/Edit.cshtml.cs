using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Requests;

public class EditModel(AppDbContext db, CurrentUserService currentUser, SubmissionService submissions, AttachmentService attachments) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public int LeaveTypeId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime StartDate { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime EndDate { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public IFormFile? Attachment { get; set; }
    [BindProperty] public bool RemoveAttachment { get; set; }
    public List<LeaveType> LeaveTypes { get; private set; } = [];
    public AbsenceRequest? RequestItem { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await currentUser.GetAsync(User);
        RequestItem = await db.AbsenceRequests.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == Id && !x.IsCancelled);
        if (RequestItem is null) return NotFound();
        if (user is null || (user.Role != "Admin" && user.EmployeeId != RequestItem.EmployeeId)) return Forbid();
        LeaveTypes = await db.LeaveTypes.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        StartDate = StartDate.Date; EndDate = EndDate.Date;
        if (EndDate < StartDate) ModelState.AddModelError(nameof(EndDate), "End date must not be earlier than start date.");
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == LeaveTypeId && x.IsActive);
        if (leaveType is null) ModelState.AddModelError(nameof(LeaveTypeId), "Select an active leave type.");
        var isSickLeave = string.Equals(leaveType?.Name, "Sick Leave", StringComparison.OrdinalIgnoreCase);
        if (Attachment is not null && !isSickLeave) ModelState.AddModelError(nameof(Attachment), "A document can only be attached to Sick Leave.");
        if (Attachment is not null && isSickLeave)
        {
            var attachmentError = attachments.Validate(Attachment);
            if (attachmentError is not null) ModelState.AddModelError(nameof(Attachment), attachmentError);
        }
        if (await db.AbsenceRequests.AnyAsync(x => x.Id != Id && x.EmployeeId == RequestItem.EmployeeId && !x.IsCancelled && x.StartDate <= EndDate && x.EndDate >= StartDate)) ModelState.AddModelError(string.Empty, "This period overlaps another active absence.");
        if (!ModelState.IsValid) return Page();
        var oldStorageName = RequestItem.AttachmentStorageName;
        StoredAttachment? replacement = null;
        try
        {
            if (Attachment is not null) replacement = await attachments.SaveAsync(Attachment);
            RequestItem.LeaveTypeId = LeaveTypeId; RequestItem.StartDate = StartDate; RequestItem.EndDate = EndDate; RequestItem.Notes = Notes?.Trim(); RequestItem.UpdatedAtUtc = DateTime.UtcNow;
            if (replacement is not null)
            {
                RequestItem.AttachmentOriginalName = replacement.OriginalName; RequestItem.AttachmentStorageName = replacement.StorageName;
                RequestItem.AttachmentContentType = replacement.ContentType; RequestItem.AttachmentSize = replacement.Size;
            }
            else if (!isSickLeave || RemoveAttachment)
            {
                RequestItem.AttachmentOriginalName = null; RequestItem.AttachmentStorageName = null;
                RequestItem.AttachmentContentType = null; RequestItem.AttachmentSize = null;
            }
            await db.SaveChangesAsync();
            if (oldStorageName is not null && (replacement is not null || !isSickLeave || RemoveAttachment)) attachments.Delete(oldStorageName);
        }
        catch
        {
            if (replacement is not null) attachments.Delete(replacement.StorageName);
            throw;
        }
        await submissions.MarkRangeAsync(RequestItem.EmployeeId, StartDate, EndDate);
        return RedirectToPage("/Index", new { month = StartDate.Month, year = StartDate.Year });
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        var user = await currentUser.GetAsync(User);
        var request = await db.AbsenceRequests.SingleOrDefaultAsync(x => x.Id == Id && !x.IsCancelled);
        if (request is null) return NotFound();
        if (user is null || (user.Role != "Admin" && user.EmployeeId != request.EmployeeId)) return Forbid();
        request.IsCancelled = true; request.CancelledByEmail = user.Email; request.CancelledAtUtc = DateTime.UtcNow; request.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await submissions.RefreshRangeAsync(request.EmployeeId, request.StartDate, request.EndDate);
        return RedirectToPage("/Index", new { month = request.StartDate.Month, year = request.StartDate.Year });
    }

    private async Task<bool> LoadAsync()
    {
        var user = await currentUser.GetAsync(User);
        RequestItem = await db.AbsenceRequests.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == Id && !x.IsCancelled);
        if (RequestItem is null || user is null || (user.Role != "Admin" && user.EmployeeId != RequestItem.EmployeeId)) return false;
        LeaveTypes = await db.LeaveTypes.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        LeaveTypeId = RequestItem.LeaveTypeId; StartDate = RequestItem.StartDate; EndDate = RequestItem.EndDate; Notes = RequestItem.Notes; return true;
    }
}
