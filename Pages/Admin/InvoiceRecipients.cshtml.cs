using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public sealed class InvoiceRecipientsModel(AppDbContext db) : PageModel
{
    public List<InvoiceRecipient> Recipients { get; private set; } = [];

    [BindProperty, StringLength(120)]
    public string? RecipientName { get; set; }

    [BindProperty, Required, EmailAddress, StringLength(320)]
    public string? RecipientEmail { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var name = (RecipientName ?? "").Trim();
        var email = (RecipientEmail ?? "").Trim().ToLowerInvariant();
        ModelState.Remove(nameof(RecipientName));
        ModelState.Remove(nameof(RecipientEmail));

        if (!new EmailAddressAttribute().IsValid(email))
            ModelState.AddModelError(nameof(RecipientEmail), "Enter a valid email.");
        if (await db.InvoiceRecipients.AnyAsync(x => x.Email == email))
            ModelState.AddModelError(nameof(RecipientEmail), "This invoice recipient already exists.");
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var isFirst = !await db.InvoiceRecipients.AnyAsync();
        db.InvoiceRecipients.Add(new InvoiceRecipient
        {
            Name = string.IsNullOrWhiteSpace(name) ? email : name,
            Email = email,
            IsActive = true,
            IsDefault = isFirst
        });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var recipient = await db.InvoiceRecipients.FindAsync(id);
        if (recipient is null) return NotFound();

        recipient.IsActive = !recipient.IsActive;
        if (!recipient.IsActive && recipient.IsDefault)
        {
            recipient.IsDefault = false;
            var replacement = await db.InvoiceRecipients
                .Where(x => x.Id != recipient.Id && x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Email)
                .FirstOrDefaultAsync();
            if (replacement is not null) replacement.IsDefault = true;
        }

        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(int id)
    {
        var recipient = await db.InvoiceRecipients.SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (recipient is null) return NotFound();

        await db.InvoiceRecipients.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false));
        recipient.IsDefault = true;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Recipients = await db.InvoiceRecipients
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Email)
            .ToListAsync();
    }
}
