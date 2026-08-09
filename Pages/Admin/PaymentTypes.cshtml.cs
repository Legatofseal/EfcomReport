using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public sealed class PaymentTypesModel(AppDbContext db) : PageModel
{
    public List<PaymentTypeOption> Options { get; private set; } = [];

    [BindProperty, Required, StringLength(100)]
    public string PaymentTypeName { get; set; } = "";

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var name = PaymentTypeName.Trim();
        ModelState.Remove(nameof(PaymentTypeName));
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(PaymentTypeName), "Enter a payment type.");
        if (await db.PaymentTypeOptions.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(PaymentTypeName), "This payment type already exists.");
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.PaymentTypeOptions.Add(new PaymentTypeOption { Name = name, IsActive = true });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var option = await db.PaymentTypeOptions.FindAsync(id);
        if (option is null) return NotFound();
        option.IsActive = !option.IsActive;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync() => Options = await db.PaymentTypeOptions
        .OrderByDescending(x => x.IsActive)
        .ThenBy(x => x.Name)
        .ToListAsync();
}
