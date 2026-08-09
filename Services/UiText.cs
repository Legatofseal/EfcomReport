using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace EfcomReport.Services;

public sealed class UiText
{
    public const string LanguageCookieName = "efcom-language";
    private static readonly Encoding Windows1252 = CreateWindows1252();

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static readonly IReadOnlyDictionary<string, string> Hebrew = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["My tracker"] = "המעקב שלי", ["Administration"] = "ניהול", ["Work calendar"] = "לוח ימי עבודה",
        ["Sign in"] = "התחברות", ["Sign out"] = "התנתקות", ["Language"] = "שפה",
        ["Employees and settings"] = "עובדים והגדרות", ["Employees"] = "עובדים", ["Full name"] = "שם מלא",
        ["Google email"] = "אימייל Google", ["Name"] = "שם", ["Email"] = "אימייל", ["Status"] = "סטטוס",
        ["Active"] = "פעיל", ["Inactive"] = "לא פעיל", ["Toggle"] = "שנה סטטוס", ["Add"] = "הוסף",
        ["Administrators"] = "מנהלים", ["Administrator name"] = "שם המנהל", ["Administrator Google email"] = "אימייל Google של המנהל",
        ["Invite administrator"] = "הזמן מנהל", ["Invite"] = "הזמן", ["Leave types"] = "סוגי היעדרות",
        ["New leave type"] = "סוג היעדרות חדש", ["Type"] = "סוג", ["Report recipients"] = "נמעני הדוחות",
        ["Reports"] = "דוחות", ["All requests"] = "כל הבקשות", ["Reminders"] = "תזכורות", ["Request a report"] = "בקש דיווח",
        ["My leave tracker"] = "המעקב שלי אחרי היעדרויות", ["Month"] = "חודש", ["Year"] = "שנה", ["Open"] = "פתח",
        ["Monthly submission"] = "דיווח חודשי", ["No absence this month"] = "לא היו היעדרויות החודש",
        ["Add absence"] = "הוסף היעדרות", ["My absences"] = "ההיעדרויות שלי", ["No absence records for this month."] = "אין רישומי היעדרות החודש.",
        ["Your Google account is not linked to an employee yet. Ask an administrator to add your email."] = "חשבון Google שלך עדיין לא מקושר לעובד. בקש ממנהל להוסיף את האימייל שלך.",
        ["Absences submitted"] = "דיווח היעדרויות הוגש", ["No absence"] = "לא היו היעדרויות", ["Did not submit this month"] = "לא הגיש דיווח החודש",
        ["Document"] = "מסמך", ["Edit"] = "עריכה", ["Download"] = "הורדה", ["Workdays are calculated from the administrator calendar."] = "ימי העבודה מחושבים לפי לוח המנהל.",
        ["Leave type"] = "סוג היעדרות", ["Select..."] = "בחר...", ["Start date"] = "תאריך התחלה", ["End date"] = "תאריך סיום",
        ["Sick-leave document"] = "מסמך מחלה", ["(optional)"] = "(אופציונלי)", ["Only for Sick Leave. PDF, JPG or PNG, up to 10 MB."] = "רק עבור Sick Leave. קובץ PDF, JPG או PNG, עד 10 MB.",
        ["Notes"] = "הערות", ["Save"] = "שמור", ["Cancel"] = "ביטול", ["Edit absence"] = "עריכת היעדרות", ["Current:"] = "נוכחי:",
        ["Remove current document"] = "הסר את המסמך הנוכחי", ["Save changes"] = "שמור שינויים", ["Cancel absence"] = "בטל היעדרות",
        ["Read-only view of the administrator calendar."] = "תצוגה לקריאה בלבד של לוח המנהל.", ["Working day"] = "יום עבודה", ["Day off"] = "יום חופשי",
        ["Manual administrator changes are marked with *."] = "שינויים ידניים של המנהל מסומנים ב-*.", ["Working"] = "עבודה", ["Off"] = "חופש",
        ["Manual administrator override"] = "שינוי ידני של המנהל", ["Default calendar rule"] = "כלל לוח ברירת מחדל",
        ["Default: Sunday–Thursday working. Click a date to create or change an override."] = "ברירת מחדל: עבודה בימים ראשון–חמישי. לחץ על תאריך כדי ליצור או לשנות חריגה.",
        ["Review missing submissions and email a report."] = "בדוק דיווחים חסרים ושלח דוח באימייל.", ["Manage employees, leave types and report recipients."] = "נהל עובדים, סוגי היעדרות ונמעני דוחות.",
        ["Mark individual dates as working or non-working."] = "סמן תאריכים בודדים כימי עבודה או כחופשה.", ["Review and correct employee absence requests."] = "בדוק ותקן בקשות היעדרות של עובדים.",
        ["Test monthly reminders for employees who have not submitted."] = "בדוק תזכורות חודשיות לעובדים שלא הגישו דיווח.", ["Email a specific employee and ask for a monthly submission."] = "שלח לעובד מסוים בקשה לדיווח חודשי.",
        ["Request a report from an employee"] = "בקש דיווח מעובד", ["The selected employee receives an email asking them to submit the selected month, or confirm that there was no absence."] = "העובד שנבחר יקבל אימייל עם בקשה לדווח עבור החודש שנבחר או לאשר שלא היו היעדרויות.",
        ["Employee"] = "עובד", ["Send"] = "שלח", ["Recent requests"] = "בקשות אחרונות", ["Period"] = "תקופה", ["Sent to"] = "נשלח אל", ["Requested at"] = "נשלח בתאריך", ["By"] = "על ידי",
        ["Download CSV"] = "הורד CSV", ["Send report"] = "שלח דוח", ["Send selected recipients"] = "שלח לנמענים שנבחרו", ["All absence requests"] = "כל בקשות ההיעדרות", ["Submitted by"] = "הוגש על ידי",
        ["Monthly reminders"] = "תזכורות חודשיות", ["The background worker checks the configured day of month. Use this button to test the reminder run locally."] = "שירות הרקע בודק את היום שהוגדר בחודש. השתמש בכפתור כדי לבדוק את הפעלת התזכורת.", ["Run reminder check now"] = "בדוק תזכורות עכשיו",
        ["Error."] = "שגיאה.", ["An error occurred while processing your request."] = "אירעה שגיאה בעת עיבוד הבקשה.", ["Privacy Policy"] = "מדיניות פרטיות", ["Use this page to detail your site's privacy policy."] = "השתמש בדף זה כדי לפרט את מדיניות הפרטיות."
    };

    private static readonly IReadOnlyDictionary<string, string> AdditionalHebrew = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Tracker"] = "\u05DE\u05E2\u05E7\u05D1", ["Invoices"] = "\u05D7\u05E9\u05D1\u05D5\u05E0\u05D5\u05EA", ["Invoice entries"] = "\u05E8\u05E9\u05D5\u05DE\u05D5\u05EA \u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD", ["New invoice entry"] = "\u05E8\u05E9\u05D5\u05DE\u05EA \u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD \u05D7\u05D3\u05E9\u05D4",
        ["Invoice recipients"] = "\u05E0\u05DE\u05E2\u05E0\u05D9 \u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD", ["Choose who receives invoice emails from the Invoices module."] = "\u05D1\u05D7\u05E8 \u05DE\u05D9 \u05E7\u05D5\u05D1\u05DC \u05D3\u05D5\u05D0\u05E8\u05D9\u05DD \u05E2\u05DC \u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD.",
        ["Add invoice recipient"] = "\u05D4\u05D5\u05E1\u05E3 \u05E0\u05DE\u05E2\u05DF \u05D7\u05E9\u05D1\u05D5\u05DF", ["Recipient name"] = "\u05E9\u05DD \u05D4\u05E0\u05DE\u05E2\u05DF", ["Invoice recipient email"] = "\u05D3\u05D5\u05D0\u05E8 \u05D0\u05DC\u05E7\u05D8\u05E8\u05D5\u05E0\u05D9 \u05E9\u05DC \u05E0\u05DE\u05E2\u05DF \u05D4\u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD", ["Configured recipients"] = "\u05E0\u05DE\u05E2\u05E0\u05D9\u05DD \u05DE\u05D5\u05D2\u05D3\u05E8\u05D9\u05DD",
        ["Default"] = "\u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC", ["Set default"] = "\u05D4\u05D2\u05D3\u05E8 \u05DB\u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC", ["Select invoice recipient"] = "\u05D1\u05D7\u05E8 \u05E0\u05DE\u05E2\u05DF \u05D7\u05E9\u05D1\u05D5\u05DF",
        ["No invoice recipients configured yet."] = "\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D0 \u05D4\u05D5\u05D2\u05D3\u05E8\u05D5 \u05E0\u05DE\u05E2\u05E0\u05D9 \u05D7\u05E9\u05D1\u05D5\u05E0\u05D9\u05DD.", ["Yes"] = "\u05DB\u05DF", ["No active invoice recipients are configured. Ask an administrator to add one."] = "\u05DC\u05D0 \u05D4\u05D5\u05D2\u05D2\u05E8 \u05E0\u05DE\u05E2\u05DF \u05D7\u05E9\u05D1\u05D5\u05DF \u05E4\u05E2\u05D9\u05DC. \u05D1\u05E7\u05E9 \u05DE\u05DE\u05E0\u05D4\u05DC \u05DC\u05D4\u05D5\u05E1\u05D9\u05E3 \u05E0\u05DE\u05E2\u05DF.",
        ["Start month"] = "\u05D7\u05D5\u05D3\u05E9 \u05D4\u05EA\u05D7\u05DC\u05D4", ["Start year"] = "\u05E9\u05E0\u05EA \u05D4\u05EA\u05D7\u05DC\u05D4", ["End month"] = "\u05D7\u05D5\u05D3\u05E9 \u05E1\u05D9\u05D5\u05DD", ["End year"] = "\u05E9\u05E0\u05EA \u05E1\u05D9\u05D5\u05DD",
        ["Apply filters"] = "\u05D4\u05D7\u05DC \u05DE\u05E1\u05E0\u05E0\u05D9\u05DD", ["By default, all active employees are included."] = "\u05DB\u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC, \u05DB\u05DC \u05D4\u05E2\u05D5\u05D1\u05D3\u05D9\u05DD \u05D4\u05E4\u05E2\u05D9\u05DC\u05D9\u05DD \u05E0\u05DB\u05DC\u05DC\u05D9\u05DD.", ["Report period"] = "\u05EA\u05E7\u05D5\u05E4\u05EA \u05D4\u05D3\u05D5\u05D7",
        ["Days"] = "\u05D9\u05DE\u05D9\u05DD", ["Entries"] = "\u05E8\u05E9\u05D5\u05DE\u05D5\u05EA", ["Total leave days"] = "\u05E1\u05DA \u05D9\u05DE\u05D9 \u05D4\u05D4\u05D9\u05E2\u05D3\u05E8\u05D5\u05EA", ["Submission status"] = "\u05E1\u05D8\u05D8\u05D5\u05E1 \u05D3\u05D9\u05D5\u05D5\u05D7",
        ["Request reports from employees"] = "\u05D1\u05E7\u05E9 \u05D3\u05D9\u05D5\u05D5\u05D7\u05D9\u05DD \u05DE\u05D4\u05E2\u05D5\u05D1\u05D3\u05D9\u05DD", ["Select a month to see who has not submitted. Missing submissions are selected by default."] = "\u05D1\u05D7\u05E8 \u05D7\u05D5\u05D3\u05E9 \u05DB\u05D3\u05D9 \u05DC\u05E8\u05D0\u05D5\u05EA \u05DE\u05D9 \u05DC\u05D0 \u05D4\u05D2\u05D9\u05E9. \u05D3\u05D9\u05D5\u05D5\u05D7\u05D9\u05DD \u05D7\u05E1\u05E8\u05D9\u05DD \u05E0\u05D1\u05D7\u05E8\u05D9\u05DD \u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC.",
        ["Missing submissions are highlighted and selected by default."] = "\u05D3\u05D9\u05D5\u05D5\u05D7\u05D9\u05DD \u05D7\u05E1\u05E8\u05D9\u05DD \u05DE\u05D5\u05D3\u05D2\u05E9\u05D9\u05DD \u05D5\u05E0\u05D1\u05D7\u05E8\u05D9\u05DD \u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC.", ["Request reports from selected employees"] = "\u05D1\u05E7\u05E9 \u05D3\u05D9\u05D5\u05D5\u05D7\u05D9\u05DD \u05DE\u05D4\u05E2\u05D5\u05D1\u05D3\u05D9\u05DD \u05E9\u05E0\u05D1\u05D7\u05E8\u05D5", ["Email selected employees and ask for a monthly submission."] = "\u05E9\u05DC\u05D7 \u05DC\u05E2\u05D5\u05D1\u05D3\u05D9\u05DD \u05E9\u05E0\u05D1\u05D7\u05E8\u05D5 \u05D1\u05E7\u05E9\u05D4 \u05DC\u05D3\u05D9\u05D5\u05D5\u05D7 \u05D7\u05D5\u05D3\u05E9\u05D9.", ["No employees selected."] = "\u05DC\u05D0 \u05E0\u05D1\u05D7\u05E8\u05D5 \u05E2\u05D5\u05D1\u05D3\u05D9\u05DD"
    };

    private static readonly IReadOnlyDictionary<string, string> NewHebrew = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Confirmed"] = "\\u05D0\\u05D5\\u05E9\\u05E8", ["Not confirmed"] = "\\u05DC\\u05D0 \\u05D0\\u05D5\\u05E9\\u05E8", ["Confirm monthly report"] = "\\u05D0\\u05E9\\u05E8 \\u05D3\\u05D9\\u05D5\\u05D5\\u05D7 \\u05D7\\u05D5\\u05D3\\u05E9\\u05D9", ["Download report package"] = "\\u05D4\\u05D5\\u05E8\\u05D3 \\u05D7\\u05D1\\u05D9\\u05DC\\u05EA \\u05D3\\u05D5\\u05D7", ["Remind"] = "\\u05D4\\u05D6\\u05DB\\u05E8",
        ["Payment types"] = "\\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["Manage the payment types available when submitting an invoice."] = "\\u05E0\\u05D4\\u05DC \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05D6\\u05DE\\u05D9\\u05E0\\u05D9\\u05DD \\u05D1\\u05D4\\u05D2\\u05E9\\u05EA \\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA.", ["Add payment type"] = "\\u05D4\\u05D5\\u05E1\\u05E3 \\u05E1\\u05D5\\u05D2 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["Configured payment types"] = "\\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05DE\\u05D5\\u05D2\\u05D3\\u05E8\\u05D9\\u05DD", ["No payment types configured yet."] = "\\u05E2\\u05D3\\u05D9\\u05D9\\u05DF \\u05DC\\u05D0 \\u05D4\\u05D5\\u05D2\\u05D3\\u05E8\\u05D5 \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD.",
        ["Select payment type"] = "\\u05D1\\u05D7\\u05E8 \\u05E1\\u05D5\\u05D2 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["No active payment types are configured. Ask an administrator to add one."] = "\\u05DC\\u05D0 \\u05D4\\u05D5\\u05D2\\u05D3\\u05E8\\u05D5 \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05E4\\u05E2\\u05D9\\u05DC\\u05D9\\u05DD.", ["Placeholder - not a real invoice"] = "\\u05DE\\u05D9\\u05D5\\u05E6\\u05D2 \\u05DC\\u05D0 \\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA \\u05D0\\u05DE\\u05D9\\u05EA\\u05D9\\u05EA", ["Placeholder"] = "\\u05DE\\u05D9\\u05D5\\u05E6\\u05D2", ["Kind"] = "\\u05E1\\u05D5\\u05D2", ["Invoice"] = "\\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA", ["Sent by email only"] = "\\u05E0\\u05E9\\u05DC\\u05D7 \\u05D1\\u05D3\\u05D5\\u05D0\\u05E8 \\u05D1\\u05DC\\u05D1\\u05D3"
    };

    public string Language(IHttpContextAccessor context) => Normalize(context.HttpContext?.Request.Cookies[LanguageCookieName]);
    public string Direction(IHttpContextAccessor context) => Language(context) == "he" ? "rtl" : "ltr";
    public string Get(IHttpContextAccessor context, string value)
    {
        if (Language(context) != "he") return value;
        if (!Hebrew.TryGetValue(value, out var translation) && !AdditionalHebrew.TryGetValue(value, out translation) && !NewHebrew.TryGetValue(value, out translation))
            return value;
        return DecodeTranslation(translation);
    }

    private static string DecodeTranslation(string value)
    {
        var decoded = DecodeUnicodeEscapes(value);
        if (!decoded.Contains('×') && !decoded.Contains('â') && !decoded.Contains('ð')) return decoded;
        try
        {
            var repaired = Encoding.UTF8.GetString(Windows1252.GetBytes(decoded));
            return repaired.Contains('\uFFFD') ? decoded : repaired;
        }
        catch (DecoderFallbackException)
        {
            return decoded;
        }
    }

    private static string DecodeUnicodeEscapes(string value) =>
        Regex.Replace(value, @"\\u([0-9a-fA-F]{4})", match =>
            ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(),
            RegexOptions.CultureInvariant);

    public string MonthName(IHttpContextAccessor context, int month)
    {
        if (month is < 1 or > 12) return month.ToString(CultureInfo.InvariantCulture);
        if (Language(context) == "he")
            return new[] { "\u05D9\u05E0\u05D5\u05D0\u05E8", "\u05E4\u05D1\u05E8\u05D5\u05D0\u05E8", "\u05DE\u05E8\u05E5", "\u05D0\u05E4\u05E8\u05D9\u05DC", "\u05DE\u05D0\u05D9", "\u05D9\u05D5\u05E0\u05D9", "\u05D9\u05D5\u05DC\u05D9", "\u05D0\u05D5\u05D2\u05D5\u05E1\u05D8", "\u05E1\u05E4\u05D8\u05DE\u05D1\u05E8", "\u05D0\u05D5\u05E7\u05D8\u05D5\u05D1\u05E8", "\u05E0\u05D5\u05D1\u05DE\u05D1\u05E8", "\u05D3\u05E6\u05DE\u05D1\u05E8" }[month - 1];
        if (Language(context) == "he") return new[] { "ינואר", "פברואר", "מרץ", "אפריל", "מאי", "יוני", "יולי", "אוגוסט", "ספטמבר", "אוקטובר", "נובמבר", "דצמבר" }[month - 1];
        return CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(month);
    }

    public string LanguageUrl(IHttpContextAccessor context, string language)
    {
        var httpContext = context.HttpContext;
        var returnUrl = httpContext is null ? "/" : $"{httpContext.Request.PathBase}{httpContext.Request.Path}{httpContext.Request.QueryString}";
        return $"/language/set?language={Uri.EscapeDataString(Normalize(language))}&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    public static string Normalize(string? language) => string.Equals(language, "he", StringComparison.OrdinalIgnoreCase) ? "he" : "en";
}
