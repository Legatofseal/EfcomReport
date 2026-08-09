using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Invoices;

public sealed class IndexModel(AppDbContext db, CurrentUserService currentUser, InvoiceService invoices) : PageModel
{
    public List<InvoiceEntry> Entries { get; private set; } = [];
    public bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await currentUser.GetAsync(User);
        if (user is null) return Forbid();
        if (user.Role != "Admin") return Forbid();

        var query = db.InvoiceEntries.AsNoTracking();

        Entries = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync(int id)
    {
        var user = await currentUser.GetAsync(User);
        if (user is null || user.Role != "Admin") return Forbid();

        var entry = await db.InvoiceEntries.SingleOrDefaultAsync(x => x.Id == id);
        if (entry is null) return NotFound();
        if (entry.EmailSentAtUtc.HasValue)
        {
            TempData["Message"] = "This invoice email has already been sent.";
            return RedirectToPage();
        }

        var result = await invoices.ResendAsync(entry, HttpContext.RequestAborted);
        TempData["Message"] = result.EmailSent
            ? "Invoice email sent again."
            : "Invoice email could not be sent again. Check the error in the entry or email configuration.";
        return RedirectToPage();
    }
}
