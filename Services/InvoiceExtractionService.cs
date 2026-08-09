using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EfcomReport.Services;

public sealed record InvoiceExtractionResult(
    string? Customer,
    string? InvoiceNumber,
    string? CurrencySymbol,
    decimal? Amount,
    string Source,
    string Message,
    IReadOnlyList<string> Warnings);

public sealed class InvoiceExtractionService(
    IConfiguration configuration,
    ILogger<InvoiceExtractionService> logger)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);
    private static readonly string[] InvoiceNumberLabels = ["Invoice Number", "Invoice No", "Invoice #", "Invoice", "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA"];
    private static readonly string[] CustomerLabels = ["Customer", "Client", "Bill To", "Billed To", "Vendor", "Supplier", "\u05DC\u05E7\u05D5\u05D7", "\u05E1\u05E4\u05E7"];
    private static readonly string[] AmountLabels = ["Total Due", "Amount Due", "Grand Total", "Balance Due", "Total Amount", "Total", "\u05E1\u05D4\u0022\u05DB", "\u05E1\u05D4\u05F4\u05DB", "\u05E1\u05DA \u05D4\u05DB\u05DC", "\u05DC\u05EA\u05E9\u05DC\u05D5\u05DD"];

    public async Task<InvoiceExtractionResult> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var temporaryDirectory = Directory.CreateTempSubdirectory("efcom-invoice-");
        var inputPath = Path.Combine(temporaryDirectory.FullName, $"input{extension}");
        try
        {
            await using (var stream = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(stream, cancellationToken);

            var warnings = new List<string>();
            var source = "manual fallback";
            var text = "";
            if (extension == ".pdf")
            {
                text = await ExtractPdfTextAsync(inputPath, temporaryDirectory.FullName, warnings, cancellationToken);
                source = string.IsNullOrWhiteSpace(text) ? "OCR/manual fallback" : "PDF text";
            }
            else
            {
                text = await ExtractImageTextAsync(inputPath, warnings, cancellationToken);
                source = "OCR/manual fallback";
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                warnings.Add("No readable text was found. Enter the fields manually.");
                return new InvoiceExtractionResult(null, null, null, null, source,
                    "The document could not be read automatically. Please enter the fields manually.", warnings);
            }

            var result = Parse(text, source, warnings);
            logger.LogInformation("Invoice extraction found {FieldCount} fields from {Source}",
                CountFields(result), source);
            return result;
        }
        finally
        {
            try { temporaryDirectory.Delete(true); }
            catch (Exception ex) { logger.LogDebug(ex, "Could not remove temporary invoice extraction directory"); }
        }
    }

    private async Task<string> ExtractPdfTextAsync(
        string inputPath,
        string temporaryDirectory,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var textPath = Path.Combine(temporaryDirectory, "extracted.txt");
        var textResult = await RunCommandAsync(
            ToolPath("InvoiceExtraction:PdfToTextPath", "pdftotext"),
            ["-layout", inputPath, textPath],
            cancellationToken);
        if (textResult?.ExitCode == 0 && File.Exists(textPath))
        {
            var text = await File.ReadAllTextAsync(textPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        if (textResult is null)
            warnings.Add("PDF text extraction is not available on this host.");
        else if (!string.IsNullOrWhiteSpace(textResult.Error))
            warnings.Add("The PDF did not contain directly readable text; OCR was attempted.");

        var imagePrefix = Path.Combine(temporaryDirectory, "page");
        var rasterResult = await RunCommandAsync(
            ToolPath("InvoiceExtraction:PdfToPpmPath", "pdftoppm"),
            ["-f", "1", "-l", "3", "-png", inputPath, imagePrefix],
            cancellationToken);
        if (rasterResult is null || rasterResult.ExitCode != 0)
        {
            warnings.Add("The PDF could not be converted to images for OCR.");
            return "";
        }

        var pages = Directory.GetFiles(temporaryDirectory, "page-*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (pages.Count == 0)
        {
            warnings.Add("No PDF pages were available for OCR.");
            return "";
        }

        var textParts = new List<string>();
        foreach (var page in pages)
        {
            var pageText = await ExtractImageTextAsync(page, warnings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(pageText)) textParts.Add(pageText);
        }
        return string.Join(Environment.NewLine, textParts);
    }

    private async Task<string> ExtractImageTextAsync(
        string inputPath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var result = await RunCommandAsync(
            ToolPath("InvoiceExtraction:TesseractPath", "tesseract"),
            [inputPath, "stdout", "-l", configuration["InvoiceExtraction:TesseractLanguages"] ?? "eng+heb+rus"],
            cancellationToken);
        if (result is null)
        {
            warnings.Add("OCR is not installed on this host. Upload a text PDF or enter the fields manually.");
            return "";
        }
        if (result.ExitCode != 0)
        {
            warnings.Add("OCR could not read this document. Please check the fields manually.");
            return "";
        }
        return result.Output;
    }

    private InvoiceExtractionResult Parse(string text, string source, List<string> warnings)
    {
        var customer = ExtractLabeledValue(text, CustomerLabels);
        var invoiceNumber = ExtractLabeledValue(text, InvoiceNumberLabels);
        var amountLine = ExtractLabeledValue(text, AmountLabels);
        var money = ParseMoney(amountLine ?? text);
        var currency = money.Currency ?? FindCurrency(text);
        var amount = money.Amount;

        if (customer is null) warnings.Add("Customer was not detected.");
        if (invoiceNumber is null) warnings.Add("Invoice number was not detected.");
        if (currency is null) warnings.Add("Currency was not detected.");
        if (amount is null) warnings.Add("Total amount was not detected.");

        var found = new[] { customer, invoiceNumber, currency, amount?.ToString(CultureInfo.InvariantCulture) }
            .Count(x => !string.IsNullOrWhiteSpace(x));
        return new InvoiceExtractionResult(
            customer,
            invoiceNumber,
            currency,
            amount,
            source,
            found == 0
                ? "Text was found, but the invoice fields could not be identified. Please enter them manually."
                : $"Detected {found} field(s) from {source}. Check the values before submitting.",
            warnings);
    }

    private static string? ExtractLabeledValue(string text, IEnumerable<string> labels)
    {
        var labelPattern = string.Join("|", labels.OrderByDescending(x => x.Length).Select(Regex.Escape));
        var match = Regex.Match(text,
            $"(?im)^\\s*(?:{labelPattern})\\s*(?:number|no\\.?|#)?\\s*[:#\\-]\\s*(?<value>[^\\r\\n]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var value = Regex.Replace(match.Groups["value"].Value.Trim(), @"\s+", " ");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static (decimal? Amount, string? Currency) ParseMoney(string text)
    {
        var currency = FindCurrency(text);
        var numberMatch = Regex.Match(text, @"(?<![A-Za-z])\(?-?\s*\d[\d\s.,]*\)?", RegexOptions.CultureInvariant);
        if (!numberMatch.Success) return (null, currency);

        var raw = numberMatch.Value.Trim();
        var negative = raw.StartsWith('(') && raw.EndsWith(')');
        raw = raw.Trim('(', ')', ' ', '\t');
        var lastComma = raw.LastIndexOf(',');
        var lastDot = raw.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var thousandsSeparator = decimalSeparator == ',' ? '.' : ',';
            raw = raw.Replace(thousandsSeparator.ToString(), "").Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            raw = raw[(raw.LastIndexOf(',') + 1)..].Length is 1 or 2
                ? raw.Replace(".", "").Replace(',', '.')
                : raw.Replace(",", "");
        }
        else if (lastDot >= 0 && raw[(lastDot + 1)..].Length > 2)
        {
            raw = raw.Replace(".", "");
        }
        raw = raw.Replace(" ", "");
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var amount))
            return (null, currency);
        return (negative ? -amount : amount, currency);
    }

    private static string? FindCurrency(string text)
    {
        var match = Regex.Match(text, @"[$\u20AC\u00A3\u20AA]|\b(?:USD|EUR|GBP|ILS|NIS)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return match.Value.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "\u20AC",
            "GBP" => "\u00A3",
            "ILS" or "NIS" => "\u20AA",
            _ => match.Value
        };
    }

    private string ToolPath(string key, string defaultValue) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? defaultValue : configuration[key]!;

    private async Task<CommandResult?> RunCommandAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start()) return null;

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CommandTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new CommandResult(-1, "", "Command timed out.");
            }

            return new CommandResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static int CountFields(InvoiceExtractionResult result) =>
        new[] { result.Customer, result.InvoiceNumber, result.CurrencySymbol, result.Amount?.ToString(CultureInfo.InvariantCulture) }
            .Count(x => !string.IsNullOrWhiteSpace(x));

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
