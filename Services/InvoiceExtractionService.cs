using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace EfcomReport.Services;

public sealed record InvoiceExtractionResult(
    string? Customer,
    string? InvoiceNumber,
    string? CurrencySymbol,
    decimal? Amount,
    string? PaymentType,
    string? Comments,
    string Source,
    string Message,
    IReadOnlyList<string> Warnings);

public sealed class InvoiceExtractionService(
    IConfiguration configuration,
    ILogger<InvoiceExtractionService> logger)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] InvoiceNumberLabels = [
        "Invoice Number", "Invoice No", "Invoice #", "Invoice",
        "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA \u05DE\u05E1",
        "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA \u05DE\u05E1\u05E4\u05E8",
        "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA"];

    private static readonly string[] CustomerLabels = [
        "Customer", "Client", "Bill To", "Billed To", "Vendor", "Supplier",
        "\u05DC\u05E7\u05D5\u05D7", "\u05E1\u05E4\u05E7", "\u05DC\u05DB\u05D1\u05D5\u05D3", "\u05E9\u05DD \u05DC\u05E7\u05D5\u05D7"];

    private static readonly string[] AmountLabels = [
        "Total Due", "Amount Due", "Grand Total", "Balance Due", "Total Amount", "Total",
        "\u05E1\u05D4\u0022\u05DB \u05DC\u05EA\u05E9\u05DC\u05D5\u05DD",
        "\u05E1\u05DB\u05D5\u05DD \u05DB\u05D5\u05DC\u05DC",
        "\u05E1\u05D4\u0022\u05DB \u05D0\u05E9\u05E8\u05D0\u05D9",
        "\u05E1\u05D4\u0022\u05DB", "\u05E1\u05D4\u05F4\u05DB", "\u05E1\u05DA \u05D4\u05DB\u05DC", "\u05DC\u05EA\u05E9\u05DC\u05D5\u05DD"];

    private static readonly string[] PaymentLabels = [
        "Payment type", "Payment method", "Card last 4", "Last 4 digits",
        "\u05D0\u05E8\u05D1\u05E2 \u05E1\u05E4\u05E8\u05D5\u05EA", "\u05E1\u05E4\u05E8\u05D5\u05EA \u05D0\u05D7\u05E8\u05D5\u05E0\u05D5\u05EA"];

    private static readonly string[] DescriptionLabels = [
        "Description", "Item description", "\u05EA\u05D9\u05D0\u05D5\u05E8", "\u05E4\u05E8\u05D9\u05D8"];

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
                return new InvoiceExtractionResult(null, null, null, null, null, null, source,
                    "The document could not be read automatically. Please enter the fields manually.", warnings);
            }

            var result = Parse(NormalizeText(text), source, warnings);
            logger.LogInformation("Invoice extraction found {FieldCount} fields from {Source}", CountFields(result), source);
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
        var managedText = TryExtractManagedPdfText(inputPath);
        if (!string.IsNullOrWhiteSpace(managedText)) return managedText;

        var textPath = Path.Combine(temporaryDirectory, "extracted.txt");
        var textResult = await RunCommandAsync(
            ToolPath("InvoiceExtraction:PdfToTextPath", "pdftotext"),
            ["-layout", "-enc", "UTF-8", inputPath, textPath],
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

        var pages = Directory.GetFiles(temporaryDirectory, "page-*.png")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private string? TryExtractManagedPdfText(string inputPath)
    {
        try
        {
            using var document = PdfDocument.Open(inputPath);
            var text = string.Join(
                Environment.NewLine,
                document.GetPages().Select(ExtractManagedPdfPageText));
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Managed PDF text extraction failed for {InputPath}", inputPath);
            return null;
        }
    }

    private static string ExtractManagedPdfPageText(UglyToad.PdfPig.Content.Page page)
    {
        var lines = new List<(double Y, List<string> Words)>();
        foreach (var word in page.GetWords(NearestNeighbourWordExtractor.Instance))
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;

            var y = word.BoundingBox.BottomLeft.Y;
            var lineIndex = lines.FindIndex(x => Math.Abs(x.Y - y) <= 2.5);
            if (lineIndex < 0)
                lines.Add((y, [NormalizeManagedPdfWord(word.Text)]));
            else
                lines[lineIndex].Words.Add(NormalizeManagedPdfWord(word.Text));
        }

        return string.Join(
            Environment.NewLine,
            lines
                .OrderByDescending(x => x.Y)
                .Select(x => string.Join(' ', x.Words)));
    }

    private static string NormalizeManagedPdfWord(string value) =>
        Regex.Replace(
            value,
            "[\\u0590-\\u05FF\\\"׳״']+",
            match => new string(match.Value.Reverse().ToArray()),
            RegexOptions.CultureInvariant);

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
        var customer = NormalizeCustomerName(ExtractLabeledValue(text, CustomerLabels));
        var invoiceNumber = ExtractInvoiceNumber(text);
        var money = ExtractTotalMoney(text);
        var currency = money.Currency ?? FindCurrency(text);
        var paymentType = ExtractPaymentType(text);
        var comments = ExtractDescription(text);

        if (customer is null) warnings.Add("Customer was not detected.");
        if (invoiceNumber is null) warnings.Add("Invoice number was not detected.");
        if (money.Amount is null) warnings.Add("Total amount was not detected.");
        if (paymentType is null) warnings.Add("Payment reference or card digits were not detected.");
        if (comments is null) warnings.Add("Item description was not detected.");

        var found = new[] {
            customer, invoiceNumber, currency, money.Amount?.ToString(CultureInfo.InvariantCulture), paymentType, comments
        }.Count(x => !string.IsNullOrWhiteSpace(x));

        return new InvoiceExtractionResult(
            customer,
            invoiceNumber,
            currency,
            money.Amount,
            paymentType,
            comments,
            source,
            found == 0
                ? "Text was found, but the invoice fields could not be identified. Please enter them manually."
                : $"Detected {found} field(s) from {source}. Check the values before submitting.",
            warnings);
    }

    private static string? ExtractInvoiceNumber(string text)
    {
        var candidates = new List<(int Score, string Number)>();
        foreach (var line in Lines(text))
        {
            if (!ContainsAny(line, "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA", "Invoice", "invoice")) continue;
            if (ContainsAny(line, "\u05DE\u05E1 \u05D4\u05D6\u05DE\u05E0\u05D4", "Order number", "Order #")) continue;

            var score = ContainsAny(line,
                "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA \u05DE\u05E1",
                "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA \u05DE\u05E1\u05E4\u05E8",
                "Invoice Number", "Invoice No", "Invoice #") ||
                (ContainsAny(line, "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA") && ContainsAny(line, "\u05DE\u05E1"))
                ? 120
                : 100;
            if (ContainsAny(line, "\u05E4\u05EA\u05E7 \u05D4\u05D7\u05DC\u05E4\u05D4", "replacement")) score -= 20;

            foreach (Match match in Regex.Matches(line, @"(?<![\d./])\d{4,12}(?![\d./])", RegexOptions.CultureInvariant))
                candidates.Add((score, match.Value));
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Number.Length)
            .Select(x => x.Number)
            .FirstOrDefault();
    }

    private static string? ExtractLabeledValue(string text, IEnumerable<string> labels)
    {
        var labelPattern = string.Join("|", labels.OrderByDescending(x => x.Length).Select(Regex.Escape));
        var lines = Lines(text).ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            foreach (var label in labels.OrderByDescending(x => x.Length))
            {
                var labelIndex = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (labelIndex < 0) continue;
                var prefix = line[..labelIndex].Trim();
                if (prefix.Length > 0 && prefix.Any(character => !":#-;|".Contains(character)))
                    continue;

                var afterLabel = line[(labelIndex + label.Length)..]
                    .TrimStart(' ', '\t', ':', '#', '-', ';', '|');
                var directValue = CleanValue(afterLabel);
                if (directValue is not null && !LooksLikeAnotherLabel(directValue))
                    return directValue;
            }

            var inline = Regex.Match(line,
                $"(?i)(?:^|\\s)(?:{labelPattern})\\s*(?:number|no\\.?|#)?\\s*[:#\\-]\\s*(?<value>.+)$",
                RegexOptions.CultureInvariant);
            if (inline.Success)
            {
                var value = CleanValue(inline.Groups["value"].Value);
                if (value is not null) return value;
            }

            var trailing = Regex.Match(line,
                $"(?i)^(?<value>.+?)\\s+(?:{labelPattern})\\s*(?:number|no\\.?|#)?\\s*[:#\\-]\\s*$",
                RegexOptions.CultureInvariant);
            if (trailing.Success)
            {
                var value = CleanValue(trailing.Groups["value"].Value);
                if (value is not null) return value;
            }

            if (Regex.IsMatch(line, $"(?i)^(?:{labelPattern})\\s*[:#\\-]?\\s*$", RegexOptions.CultureInvariant))
            {
                for (var next = index + 1; next < lines.Count && next <= index + 2; next++)
                {
                    var value = CleanValue(lines[next]);
                    if (value is not null && !LooksLikeAnotherLabel(value)) return value;
                }
            }
        }

        return null;
    }

    private static (decimal? Amount, string? Currency) ExtractTotalMoney(string text)
    {
        var candidates = new List<MoneyCandidate>();
        foreach (var line in Lines(text))
        {
            var labelScore = ContainsAny(line, "\u05DE\u05E1 \u05D4\u05D6\u05DE\u05E0\u05D4", "Order number", "Order #")
                ? 0
                : AmountLabelScore(line);
            foreach (var match in MoneyMatches(line))
            {
                var parsed = ParseMoney(match.Value);
                if (parsed.Amount is not null)
                    candidates.Add(new MoneyCandidate(labelScore + (match.Groups["currency"].Success ? 10 : 0), parsed.Amount.Value, parsed.Currency));
            }
        }

        var best = candidates
            .Where(x => x.Amount >= 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Amount)
            .FirstOrDefault();
        if (best is not null && best.Score > 0) return (best.Amount, best.Currency);

        best = candidates
            .Where(x => x.Amount >= 0 && x.Currency is not null)
            .OrderByDescending(x => x.Amount)
            .FirstOrDefault();
        return best is null ? (null, null) : (best.Amount, best.Currency);
    }

    private static string? ExtractPaymentType(string text)
    {
        var lines = Lines(text).ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!ContainsAny(line, "4 \u05E1\u05E4\u05E8\u05D5\u05EA", "4\u05E1\u05E4\u05E8\u05D5\u05EA", "\u05E1\u05E4\u05E8\u05D5\u05EA \u05D0\u05D7\u05E8\u05D5\u05E0\u05D5\u05EA", "Last 4", "last four", "Card last")) continue;
            for (var next = index; next < lines.Count && next <= index + 2; next++)
            {
                var match = Regex.Match(lines[next], @"(?<!\d)\d{4}(?!\d)", RegexOptions.CultureInvariant);
                if (match.Success) return match.Value;
            }
        }

        foreach (var line in Lines(text))
        {
            if (!ContainsAny(line, "Diners", "Visa", "Mastercard", "American Express", "\u05D0\u05E9\u05E8\u05D0\u05D9", "\u05DB\u05E8\u05D8\u05D9\u05E1"))
                continue;

            var cardMatch = Regex.Match(line, @"(?i)(?:Diners|Visa|Mastercard|American\s+Express)[^\d]{0,8}(?<digits>\d{4})");
            if (cardMatch.Success) return cardMatch.Groups["digits"].Value;
        }

        return ExtractLabeledValue(text, PaymentLabels);
    }

    private static string? ExtractDescription(string text)
    {
        var labeled = ExtractLabeledValue(text, DescriptionLabels);
        if (labeled is not null && !LooksLikeHeader(labeled)) return CleanDescription(labeled);

        foreach (var line in Lines(text))
        {
            if (LooksLikeHeader(line)) continue;
            var product = Regex.Match(line,
                @"(?<value>[A-Za-z][A-Za-z0-9'’ ._-]*\b(?:basketball|ball)\b)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (product.Success) return CleanDescription(product.Groups["value"].Value);

            if (!Regex.IsMatch(line, @"(?i)\b(?:basketball|ball)\b", RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(line, @"^\d+\s+.+\s+\d{8,}$", RegexOptions.CultureInvariant)) continue;

            var quantity = Regex.Match(line, @"(?:^|\s)\d+\s+(?<value>.+)$", RegexOptions.CultureInvariant);
            var value = quantity.Success ? quantity.Groups["value"].Value : line;
            value = Regex.Replace(value, @"\s+\d{8,}\s*$", "", RegexOptions.CultureInvariant);
            value = Regex.Replace(value, @"\s+[A-Z0-9]*\d[A-Z0-9]*\s*$", "", RegexOptions.CultureInvariant);
            value = CleanDescription(value);
            if (value is not null) return value;
        }

        return null;
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
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount))
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

    private static string? NormalizeCustomerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        value = value.Trim();
        if (value.StartsWith("בעמ ", StringComparison.Ordinal))
            return $"{value[4..]} בעמ";
        if (value.StartsWith("בע\"מ ", StringComparison.Ordinal))
            return $"{value[5..]} בע\"מ";
        return value;
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
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
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
        new[] { result.Customer, result.InvoiceNumber, result.CurrencySymbol,
            result.Amount?.ToString(CultureInfo.InvariantCulture), result.PaymentType, result.Comments }
            .Count(x => !string.IsNullOrWhiteSpace(x));

    private static IEnumerable<string> Lines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => RemoveBidiMarks(x))
            .Select(x => Regex.Replace(x, @"\s+", " ", RegexOptions.CultureInvariant).Trim())
            .Where(x => x.Length > 0);

    private static string NormalizeText(string text) => string.Join(Environment.NewLine, Lines(text));

    private static string RemoveBidiMarks(string value) =>
        Regex.Replace(value, "[\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]", "", RegexOptions.CultureInvariant);

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeAnotherLabel(string value) =>
        value.EndsWith(':') || ContainsAny(value, "Invoice", "Customer", "Client", "Total", "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05EA", "\u05DC\u05E7\u05D5\u05D7", "\u05E1\u05D4\u0022\u05DB");

    private static bool LooksLikeHeader(string value) =>
        value.Length < 120 &&
        (ContainsAny(value, "\u05EA\u05D9\u05D0\u05D5\u05E8", "Description", "Item description", "\u05DB\u05DE\u05D5\u05EA", "Quantity", "\u05DE\u05D7\u05D9\u05E8", "Price") ||
         value.Contains('#', StringComparison.Ordinal));

    private static string? CleanValue(string value)
    {
        value = RemoveBidiMarks(value);
        value = Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim(' ', '\t', ':', '-', ';', '|');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? CleanDescription(string value)
    {
        value = CleanValue(value) ?? "";
        value = Regex.Replace(value, @"^\d+\s+", "", RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\s+\d{8,}\s*$", "", RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\s+[A-Z0-9]*\d[A-Z0-9]*\s*$", "", RegexOptions.CultureInvariant);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int AmountLabelScore(string line)
    {
        if (ContainsAny(line, "\u05E1\u05D4\u0022\u05DB \u05DC\u05EA\u05E9\u05DC\u05D5\u05DD", "\u05DC\u05EA\u05E9\u05DC\u05D5\u05DD \u05E1\u05D4\u0022\u05DB", "Total Due", "Amount Due", "Balance Due")) return 130;
        if (ContainsAny(line, "\u05E1\u05DB\u05D5\u05DD \u05DB\u05D5\u05DC\u05DC", "Grand Total", "Total Amount")) return 120;
        if (ContainsAny(line, "\u05E1\u05D4\u0022\u05DB \u05D0\u05E9\u05E8\u05D0\u05D9", "\u05E1\u05DA \u05D4\u05DB\u05DC", "Total")) return 100;
        if (ContainsAny(line, "\u05E1\u05D4\u0022\u05DB", "\u05E1\u05D4\u05F4\u05DB", "\u05DC\u05EA\u05E9\u05DC\u05D5\u05DD")) return 80;
        return 0;
    }

    private static IEnumerable<Match> MoneyMatches(string line) =>
        Regex.Matches(line,
            @"(?<currency>[$\u20AC\u00A3\u20AA]|USD|EUR|GBP|ILS|NIS)?\s*(?<amount>\(?-?(?:\d+|\d{1,3}(?:\s\d{3})+)(?:[.,]\d{1,3})?\)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Cast<Match>();

    private sealed record MoneyCandidate(int Score, decimal Amount, string? Currency);
    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
