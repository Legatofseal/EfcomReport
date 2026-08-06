using EfcomReport.Data;
using EfcomReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Requests;

[Authorize]
public class AttachmentModel(AppDbContext db, CurrentUserService currentUser, AttachmentService attachments) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await currentUser.GetAsync(User);
        var request = await db.AbsenceRequests.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == id && !x.IsCancelled);
        if (request is null) return NotFound();
        if (user is null || (user.Role != "Admin" && user.EmployeeId != request.EmployeeId)) return Forbid();
        var path = attachments.GetPath(request.AttachmentStorageName);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, request.AttachmentContentType ?? "application/octet-stream", request.AttachmentOriginalName ?? "document");
    }
}
