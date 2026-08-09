using System.Globalization;
using EfcomReport.Data;
using EfcomReport.Models;

namespace EfcomReport.Services;

public sealed record InvoiceSubmissionResult(bool EmailSent);

public sealed class InvoiceService(
    AppDbContext db,
    EmailService email,
    ILogger<InvoiceService> logger)
{
    public async Task<InvoiceSubmissionResult> SubmitAsync(
        InvoiceEntry entry,
        IFormFile? attachment,
        CancellationToken cancellationToken = default)
    {
        byte[]? attachmentBytes = null;
        string? attachmentName = null;
        string? attachmentContentType = null;
        if (attachment is not null)
        {
            await using var stream = new MemoryStream();
            await attachment.CopyToAsync(stream, cancellationToken);
            attachmentBytes = stream.ToArray();
            attachmentName = Path.GetFileName(attachment.FileName);
            attachmentContentType = ContentTypeFor(Path.GetExtension(attachmentName));
        }

        // Invoice documents are deliberately never persisted. They exist only in memory
        // long enough to be attached to the outgoing email.
        entry.AttachmentOriginalName = null;
        entry.AttachmentStorageName = null;
        entry.AttachmentContentType = null;
        entry.AttachmentSize = null;
        entry.CreatedAtUtc = DateTime.UtcNow;
        db.InvoiceEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await email.SendDocumentAsync(
                [entry.RecipientEmail],
                BuildSubject(entry),
                BuildBody(entry),
                attachmentBytes,
                attachmentName,
                attachmentContentType);

            entry.EmailSentAtUtc = DateTime.UtcNow;
            entry.EmailError = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Sent invoice entry {InvoiceEntryId} to {RecipientEmail}", entry.Id, entry.RecipientEmail);
            return new InvoiceSubmissionResult(true);
        }
        catch (Exception ex)
        {
            entry.EmailError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Invoice entry {InvoiceEntryId} was saved but email delivery failed", entry.Id);
            return new InvoiceSubmissionResult(false);
        }
    }

    public static string BuildSubject(InvoiceEntry entry)
    {
        var amount = entry.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var marker = entry.IsPlaceholder ? "EFCOM_INVOICE_PLACEHOLDER" : "EFCOM_INVOICE";
        return $"{marker},[{Clean(entry.Customer)}],[{Clean(entry.InvoiceNumber)}],[{Clean(entry.CurrencySymbol)}{amount}],[{Clean(entry.PaymentType)}],[{Clean(entry.Comments)}]";
    }

    public static string BuildBody(InvoiceEntry entry)
    {
        var attachment = "Attached to this email only; it is not stored in the portal.";
        return $"New Accounting Entry\n\n" +
               $"Placeholder: {(entry.IsPlaceholder ? "Yes" : "No")}\n" +
               $"Customer: {entry.Customer}\n" +
               $"Invoice: {entry.InvoiceNumber}\n" +
               $"Amount: {entry.CurrencySymbol}{entry.Amount.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
               $"Payment: {entry.PaymentType}\n" +
               $"Comments: {entry.Comments ?? ""}\n" +
               $"Attachment: {attachment}\n" +
               $"Submitted by: {entry.SubmittedByEmail}";
    }

    private static string ContentTypeFor(string? extension) => extension?.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };

    private static string Clean(string? value) =>
        (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
}
