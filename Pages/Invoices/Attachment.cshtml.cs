using EfcomReport.Data;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Invoices;

public sealed class AttachmentModel(AppDbContext db, CurrentUserService currentUser, AttachmentService attachments) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await currentUser.GetAsync(User);
        var entry = await db.InvoiceEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (entry is null) return NotFound();
        if (user is null || user.Role != "Admin")
            return Forbid();

        var path = attachments.GetInvoicePath(entry.AttachmentStorageName);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, entry.AttachmentContentType ?? "application/octet-stream", entry.AttachmentOriginalName ?? "invoice-document");
    }
}
