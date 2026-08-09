using System.ComponentModel.DataAnnotations;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EfcomReport.Pages.Invoices;

public sealed class CreateModel(
    CurrentUserService currentUser,
    AttachmentService attachments,
    InvoiceService invoices,
    InvoiceExtractionService extraction,
    IConfiguration configuration) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(320)]
    public string RecipientEmail { get; set; } = "";

    [BindProperty, Required, StringLength(200)]
    public string Customer { get; set; } = "";

    [BindProperty, Required, StringLength(100)]
    public string InvoiceNumber { get; set; } = "";

    [BindProperty, Required, StringLength(10)]
    public string CurrencySymbol { get; set; } = "";

    [BindProperty, Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [BindProperty, Required, StringLength(100)]
    public string PaymentType { get; set; } = "";

    [BindProperty, StringLength(2000)]
    public string? Comments { get; set; }

    [BindProperty]
    public IFormFile? Attachment { get; set; }

    public void OnGet()
    {
        RecipientEmail = configuration["Invoice:DefaultRecipientEmail"] ?? "";
    }

    public async Task<IActionResult> OnPostExtractAsync(IFormFile? attachment, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(User);
        if (user is null) return Forbid();
        if (attachment is null || attachment.Length == 0)
            return BadRequest(new { message = "Choose an invoice document first." });

        var attachmentError = attachments.Validate(attachment);
        if (attachmentError is not null)
            return BadRequest(new { message = attachmentError });

        return new JsonResult(await extraction.ExtractAsync(attachment, cancellationToken));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await currentUser.GetAsync(User);
        if (user is null) return Forbid();

        RecipientEmail = RecipientEmail.Trim();
        Customer = Customer.Trim();
        InvoiceNumber = InvoiceNumber.Trim();
        CurrencySymbol = CurrencySymbol.Trim();
        PaymentType = PaymentType.Trim();
        Comments = string.IsNullOrWhiteSpace(Comments) ? null : Comments.Trim();

        if (Attachment is not null)
        {
            var attachmentError = attachments.Validate(Attachment);
            if (attachmentError is not null) ModelState.AddModelError(nameof(Attachment), attachmentError);
        }

        if (!ModelState.IsValid) return Page();

        var entry = new InvoiceEntry
        {
            SubmittedByEmail = user.Email,
            RecipientEmail = RecipientEmail,
            Customer = Customer,
            InvoiceNumber = InvoiceNumber,
            CurrencySymbol = CurrencySymbol,
            Amount = Amount,
            PaymentType = PaymentType,
            Comments = Comments
        };

        var result = await invoices.SubmitAsync(entry, Attachment, HttpContext.RequestAborted);
        TempData["Message"] = result.EmailSent
            ? "Invoice entry saved and email sent."
            : "Invoice entry saved, but email was not sent. Check the email configuration or the entry history.";
        return RedirectToPage("/Invoices/Index");
    }
}
