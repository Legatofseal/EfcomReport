using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Invoices;

public sealed class IndexModel(AppDbContext db, CurrentUserService currentUser) : PageModel
{
    public List<InvoiceEntry> Entries { get; private set; } = [];
    public bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await currentUser.GetAsync(User);
        if (user is null) return Forbid();

        var query = db.InvoiceEntries.AsNoTracking();
        if (user.Role != "Admin")
            query = query.Where(x => x.SubmittedByEmail == user.Email);

        Entries = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        return Page();
    }
}
