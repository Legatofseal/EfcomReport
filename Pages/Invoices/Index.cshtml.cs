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
    public List<InvoiceMonthSummary> MonthlySummaries { get; private set; } = [];
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

        var summaryEntries = await query
            .Select(x => new InvoiceSummaryEntry(x.CreatedAtUtc, x.CurrencySymbol, x.Amount, x.IsPlaceholder))
            .ToListAsync();
        MonthlySummaries = summaryEntries
            .GroupBy(x => new { LocalDate = x.CreatedAtUtc.ToLocalTime(), x.IsPlaceholder })
            .GroupBy(x => new { x.Key.LocalDate.Year, x.Key.LocalDate.Month })
            .OrderByDescending(x => x.Key.Year)
            .ThenByDescending(x => x.Key.Month)
            .Select(month => new InvoiceMonthSummary(
                month.Key.Year,
                month.Key.Month,
                month.Where(x => !x.Key.IsPlaceholder)
                    .SelectMany(x => x)
                    .GroupBy(x => DisplayCurrency(x.CurrencySymbol), StringComparer.Ordinal)
                    .OrderBy(x => x.Key)
                    .Select(currency => new InvoiceCurrencySummary(currency.Key, currency.Sum(x => x.Amount), currency.Count()))
                    .ToList(),
                month.Where(x => x.Key.IsPlaceholder).Sum(x => x.Count())))
            .ToList();
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

    private static string DisplayCurrency(string? currencySymbol) =>
        string.IsNullOrWhiteSpace(currencySymbol) ? "—" : currencySymbol.Trim();
}

public sealed record InvoiceSummaryEntry(DateTime CreatedAtUtc, string CurrencySymbol, decimal Amount, bool IsPlaceholder);

public sealed record InvoiceCurrencySummary(string CurrencySymbol, decimal Amount, int Count);

public sealed record InvoiceMonthSummary(
    int Year,
    int Month,
    IReadOnlyList<InvoiceCurrencySummary> Totals,
    int PlaceholderCount);
