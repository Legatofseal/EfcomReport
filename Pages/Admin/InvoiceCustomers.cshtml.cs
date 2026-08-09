using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public sealed class InvoiceCustomersModel(AppDbContext db) : PageModel
{
    public List<InvoiceCustomerOption> Customers { get; private set; } = [];

    [BindProperty, Required, StringLength(200)]
    public string? CustomerName { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var name = (CustomerName ?? "").Trim();
        ModelState.Remove(nameof(CustomerName));
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(CustomerName), "Enter a customer name.");
        if (await db.InvoiceCustomerOptions.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(CustomerName), "This invoice customer already exists.");
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.InvoiceCustomerOptions.Add(new InvoiceCustomerOption { Name = name, IsActive = true });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var customer = await db.InvoiceCustomerOptions.FindAsync(id);
        if (customer is null) return NotFound();

        customer.IsActive = !customer.IsActive;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var customer = await db.InvoiceCustomerOptions.FindAsync(id);
        if (customer is null) return NotFound();

        db.InvoiceCustomerOptions.Remove(customer);
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Customers = await db.InvoiceCustomerOptions
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }
}
