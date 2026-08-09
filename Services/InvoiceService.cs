using System.Globalization;
using EfcomReport.Data;
using EfcomReport.Models;

namespace EfcomReport.Services;

public sealed record InvoiceSubmissionResult(bool EmailSent);

public sealed class InvoiceService(
    AppDbContext db,
    AttachmentService attachments,
    EmailService email,
    ILogger<InvoiceService> logger)
{
    public async Task<InvoiceSubmissionResult> SubmitAsync(
        InvoiceEntry entry,
        IFormFile? attachment,
        CancellationToken cancellationToken = default)
    {
        StoredAttachment? storedAttachment = null;
        try
        {
            if (attachment is not null)
                storedAttachment = await attachments.SaveInvoiceAsync(attachment, cancellationToken);

            entry.AttachmentOriginalName = storedAttachment?.OriginalName;
            entry.AttachmentStorageName = storedAttachment?.StorageName;
            entry.AttachmentContentType = storedAttachment?.ContentType;
            entry.AttachmentSize = storedAttachment?.Size;
            entry.CreatedAtUtc = DateTime.UtcNow;
            db.InvoiceEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (storedAttachment is not null) attachments.DeleteInvoice(storedAttachment.StorageName);
            throw;
        }

        try
        {
            byte[]? attachmentBytes = null;
            if (!string.IsNullOrWhiteSpace(entry.AttachmentStorageName))
            {
                var path = attachments.GetInvoicePath(entry.AttachmentStorageName);
                if (path is null || !File.Exists(path))
                    throw new FileNotFoundException("The invoice attachment could not be found.");
                attachmentBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            }

            await email.SendDocumentAsync(
                [entry.RecipientEmail],
                BuildSubject(entry),
                BuildBody(entry),
                attachmentBytes,
                entry.AttachmentOriginalName,
                entry.AttachmentContentType);

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
        return string.Join(",", [
            "EFCOM_INVOICE",
            Clean(entry.Customer),
            Clean(entry.InvoiceNumber),
            $"{Clean(entry.CurrencySymbol)}{amount}",
            Clean(entry.PaymentType),
            Clean(entry.Comments)]);
    }

    public static string BuildBody(InvoiceEntry entry)
    {
        var attachment = string.IsNullOrWhiteSpace(entry.AttachmentOriginalName)
            ? "No attachment."
            : entry.AttachmentOriginalName;
        return $"New Accounting Entry\n\n" +
               $"Customer: {entry.Customer}\n" +
               $"Invoice: {entry.InvoiceNumber}\n" +
               $"Amount: {entry.CurrencySymbol}{entry.Amount.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
               $"Payment: {entry.PaymentType}\n" +
               $"Comments: {entry.Comments ?? ""}\n" +
               $"Attachment: {attachment}\n" +
               $"Submitted by: {entry.SubmittedByEmail}";
    }

    private static string Clean(string? value) =>
        (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
}
