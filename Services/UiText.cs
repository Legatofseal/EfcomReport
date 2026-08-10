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

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Tracker"] = "Трекер", ["Invoices"] = "Инвойсы", ["Invoice entries"] = "Записи инвойсов", ["New invoice entry"] = "Новая запись инвойса", ["Invoice recipients"] = "Получатели инвойсов",
        ["Administration"] = "Администрирование", ["My tracker"] = "Мой трекер", ["Work calendar"] = "Рабочий календарь", ["Sign in"] = "Войти", ["Sign out"] = "Выйти", ["Language"] = "Язык",
        ["Employees and settings"] = "Сотрудники и настройки", ["Employees"] = "Сотрудники", ["Full name"] = "Полное имя", ["Google email"] = "Email Google", ["Name"] = "Имя", ["Email"] = "Email", ["Status"] = "Статус", ["Active"] = "Активен", ["Inactive"] = "Неактивен", ["Toggle"] = "Изменить статус", ["Add"] = "Добавить",
        ["Administrators"] = "Администраторы", ["Administrator name"] = "Имя администратора", ["Administrator Google email"] = "Email Google администратора", ["Invite administrator"] = "Пригласить администратора", ["Invite"] = "Пригласить", ["Leave types"] = "Типы отсутствий", ["New leave type"] = "Новый тип отсутствия", ["Type"] = "Тип", ["Report recipients"] = "Получатели отчётов", ["Reports"] = "Отчёты", ["All requests"] = "Все запросы", ["Reminders"] = "Напоминания", ["Request a report"] = "Запросить отчёт",
        ["My leave tracker"] = "Мой трекер отсутствий", ["Month"] = "Месяц", ["Year"] = "Год", ["Open"] = "Открыть", ["Monthly submission"] = "Месячный отчёт", ["Add absence"] = "Добавить отсутствие", ["My absences"] = "Мои отсутствия", ["No absence records for this month."] = "За этот месяц нет записей об отсутствии.", ["Confirmed"] = "Подтверждён", ["Not confirmed"] = "Не подтверждён", ["Confirm monthly report"] = "Подтвердить месячный отчёт", ["Absences submitted"] = "Отсутствия отправлены", ["No absence"] = "Отсутствий нет", ["Did not submit this month"] = "Отчёт за месяц не отправлен",
        ["Document"] = "Документ", ["Edit"] = "Изменить", ["Download"] = "Скачать", ["Leave type"] = "Тип отсутствия", ["Select..."] = "Выберите...", ["Start date"] = "Дата начала", ["End date"] = "Дата окончания", ["Sick-leave document"] = "Документ о болезни", ["(optional)"] = "(необязательно)", ["Notes"] = "Примечания", ["Save"] = "Сохранить", ["Cancel"] = "Отмена", ["Edit absence"] = "Изменить отсутствие", ["Current:"] = "Текущий:", ["Remove current document"] = "Удалить текущий документ", ["Save changes"] = "Сохранить изменения", ["Cancel absence"] = "Отменить отсутствие",
        ["Read-only view of the administrator calendar."] = "Календарь доступен пользователям только для просмотра.", ["Working day"] = "Рабочий день", ["Day off"] = "Выходной", ["Working"] = "Рабочий", ["Off"] = "Выходной", ["Employee"] = "Сотрудник", ["Send"] = "Отправить", ["Recent requests"] = "Последние запросы", ["Period"] = "Период", ["Sent to"] = "Отправлено", ["Requested at"] = "Дата запроса", ["By"] = "Кем отправлено", ["Days"] = "Дни", ["Entries"] = "Записи", ["Total leave days"] = "Всего дней отсутствия", ["Submission status"] = "Статус отчёта", ["Download report package"] = "Скачать пакет отчёта", ["Send report"] = "Отправить отчёт", ["Send selected recipients"] = "Отправить выбранным получателям", ["Remind"] = "Напомнить",
        ["Payment types"] = "Типы оплаты", ["Manage the payment types available when submitting an invoice."] = "Управление типами оплаты при отправке инвойса.", ["Add payment type"] = "Добавить тип оплаты", ["Configured payment types"] = "Настроенные типы оплаты", ["No payment types configured yet."] = "Типы оплаты ещё не настроены.", ["Select payment type"] = "Выберите тип оплаты", ["No active payment types are configured. Ask an administrator to add one."] = "Нет активных типов оплаты. Попросите администратора добавить тип.", ["Placeholder - not a real invoice"] = "Placeholder — это не настоящий инвойс", ["Placeholder"] = "Placeholder", ["Kind"] = "Вид", ["Invoice"] = "Инвойс", ["Sent by email only"] = "Только отправлен по email", ["Delete"] = "Удалить", ["Delete invoice recipient?"] = "Удалить получателя инвойсов?", ["Delete month records"] = "Удалить записи за месяц", ["Available only after every active employee has confirmed this month. Multi-month absence records are retained."] = "Доступно после подтверждения месяца всеми активными сотрудниками. Записи, проходящие через несколько месяцев, сохраняются.", ["Delete all records for this month?"] = "Удалить все записи за этот месяц?", ["Waiting for confirmations"] = "Ожидание подтверждений"
    };

    private static readonly IReadOnlyDictionary<string, string> RussianAdditional = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Click to change calendar status"] = "Нажмите, чтобы изменить статус дня",
        ["Half-day absence"] = "Отсутствие на полдня",
        ["Use this for a half-day absence on one date."] = "Используйте для отсутствия на полдня в одну дату.",
        ["Day portion"] = "Продолжительность дня",
        ["Half day"] = "Полдня",
        ["Full day"] = "Полный день",
        ["Resend"] = "Отправить повторно",
        ["Resend invoice email?"] = "Повторно отправить email инвойса?",
        ["Document is not stored; resend includes only invoice data."] = "Документ не хранится; повторно отправятся только данные инвойса.",
        ["Invoice customers"] = "Клиенты инвойсов",
        ["Manage the customer list available when submitting an invoice."] = "Управление списком клиентов, доступных при создании инвойса.",
        ["Add invoice customer"] = "Добавить клиента инвойса",
        ["Customer name"] = "Название клиента",
        ["Configured invoice customers"] = "Настроенные клиенты инвойсов",
        ["No invoice customers configured yet."] = "Клиенты инвойсов ещё не настроены.",
        ["Delete invoice customer?"] = "Удалить этого клиента инвойсов? Старые записи инвойсов сохранятся.",
        ["Select invoice customer"] = "Выберите клиента инвойса",
        ["No active invoice customers are configured. Ask an administrator to add one."] = "Нет активных клиентов инвойсов. Попросите администратора добавить клиента.",
        ["Remove admin rights"] = "Убрать права администратора",
        ["Remove administrator rights?"] = "Убрать права администратора у этого пользователя? Сам пользователь останется активным.",
        ["Your Google account is not linked to an employee yet. Ask an administrator to add your email."] = "Ваш аккаунт Google ещё не связан с сотрудником. Попросите администратора добавить ваш адрес.",
        ["Workdays are calculated from the administrator calendar."] = "Рабочие дни рассчитываются по календарю администратора.",
        ["Only for Sick Leave. PDF, JPG or PNG, up to 10 MB."] = "Только для больничного. PDF или JPG/PNG, до 10 МБ.",
        ["Manual administrator changes are marked with *."] = "Ручные изменения администратора отмечены символом *.",
        ["Manual administrator override"] = "Ручное изменение администратора",
        ["Default calendar rule"] = "Правило календаря по умолчанию",
        ["Default: Sundayâ€“Thursday working. Click a date to create or change an override."] = "По умолчанию рабочие дни — с воскресенья по четверг. Нажмите на дату, чтобы создать или изменить исключение.",
        ["Manage employees, leave types and report recipients."] = "Управление сотрудниками, типами отсутствий и получателями отчётов.",
        ["Mark individual dates as working or non-working."] = "Отметить отдельные даты как рабочие или нерабочие.",
        ["Review missing submissions and email a report."] = "Проверить отсутствующие отчёты и отправить отчёт по email.",
        ["Review and correct employee absence requests."] = "Просмотр и исправление заявок сотрудников об отсутствии.",
        ["Test monthly reminders for employees who have not submitted."] = "Проверить ежемесячные напоминания сотрудникам, которые не отправили отчёт.",
        ["Email a specific employee and ask for a monthly submission."] = "Отправить выбранному сотруднику письмо с просьбой предоставить месячный отчёт.",
        ["Request a report from an employee"] = "Запросить отчёт у сотрудника",
        ["The selected employee receives an email asking them to submit the selected month, or confirm that there was no absence."] = "Выбранный сотрудник получит письмо с просьбой отправить отчёт за выбранный месяц или подтвердить отсутствие отсутствий.",
        ["Choose who receives invoice emails from the Invoices module."] = "Выберите получателей писем об инвойсах из раздела «Инвойсы».",
        ["Add invoice recipient"] = "Добавить получателя инвойсов",
        ["Recipient name"] = "Имя получателя",
        ["Invoice recipient email"] = "Email получателя инвойсов",
        ["Configured recipients"] = "Настроенные получатели",
        ["Default"] = "По умолчанию",
        ["Set default"] = "Сделать получателем по умолчанию",
        ["Select invoice recipient"] = "Выберите получателя инвойса",
        ["No invoice recipients configured yet."] = "Получатели инвойсов ещё не настроены.",
        ["Yes"] = "Да",
        ["No active invoice recipients are configured. Ask an administrator to add one."] = "Нет активных получателей инвойсов. Попросите администратора добавить получателя.",
        ["Start month"] = "Начальный месяц",
        ["Start year"] = "Начальный год",
        ["End month"] = "Конечный месяц",
        ["End year"] = "Конечный год",
        ["Apply filters"] = "Применить фильтры",
        ["By default, all active employees are included."] = "По умолчанию включены все активные сотрудники.",
        ["Report period"] = "Период отчёта",
        ["Request reports from employees"] = "Запросить отчёты у сотрудников",
        ["Select a month to see who has not submitted. Missing submissions are selected by default."] = "Выберите месяц, чтобы увидеть, кто не отправил отчёт. Отсутствующие отчёты выбраны по умолчанию.",
        ["Missing submissions are highlighted and selected by default."] = "Отсутствующие отчёты выделены и выбраны по умолчанию.",
        ["Request reports from selected employees"] = "Запросить отчёты у выбранных сотрудников",
        ["Email selected employees and ask for a monthly submission."] = "Отправить выбранным сотрудникам письмо с просьбой предоставить месячный отчёт.",
        ["No employees selected."] = "Сотрудники не выбраны.",
        ["Monthly reminders"] = "Ежемесячные напоминания",
        ["The background worker checks the configured day of month. Use this button to test the reminder run locally."] = "Фоновая служба проверяет заданный день месяца. Используйте эту кнопку для локальной проверки напоминаний.",
        ["Run reminder check now"] = "Проверить напоминания сейчас",
        ["Submit an invoice entry and send it by email with its document. The document is not stored in the portal."] = "Создайте запись инвойса и отправьте её по email вместе с документом. Документ не сохраняется в портале.",
        ["Recipient email"] = "Email получателя",
        ["Customer"] = "Клиент",
        ["Invoice number"] = "Номер инвойса",
        ["Currency symbol"] = "Символ валюты",
        ["optional"] = "необязательно",
        ["Amount"] = "Сумма",
        ["Payment type / reference"] = "Тип оплаты / ссылка",
        ["Payment type"] = "Тип оплаты",
        ["Invoice document"] = "Документ инвойса",
        ["PDF, JPG or PNG, up to 10 MB."] = "PDF, JPG или PNG, до 10 МБ.",
        ["Try to fill from document"] = "Попробовать заполнить из документа",
        ["Comments"] = "Комментарии",
        ["Submit invoice"] = "Отправить инвойс",
        ["All submitted invoice entries."] = "Все отправленные записи инвойсов.",
        ["Your submitted invoice entries."] = "Ваши отправленные записи инвойсов.",
        ["No invoice entries yet."] = "Записей инвойсов пока нет.",
        ["Date"] = "Дата",
        ["Sent"] = "Отправлен",
        ["Saved; email not sent"] = "Сохранён; email не отправлен",
        ["Submitted by"] = "Отправил",
        ["All absence requests"] = "Все заявки об отсутствии",
        ["Privacy Policy"] = "Политика конфиденциальности",
        ["Use this page to detail your site's privacy policy."] = "Используйте эту страницу для описания политики конфиденциальности сайта.",
        ["Error."] = "Ошибка.",
        ["An error occurred while processing your request."] = "При обработке запроса произошла ошибка."
    };

    private static readonly IReadOnlyDictionary<string, string> NewHebrew = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Confirmed"] = "\\u05D0\\u05D5\\u05E9\\u05E8", ["Not confirmed"] = "\\u05DC\\u05D0 \\u05D0\\u05D5\\u05E9\\u05E8", ["Confirm monthly report"] = "\\u05D0\\u05E9\\u05E8 \\u05D3\\u05D9\\u05D5\\u05D5\\u05D7 \\u05D7\\u05D5\\u05D3\\u05E9\\u05D9", ["Download report package"] = "\\u05D4\\u05D5\\u05E8\\u05D3 \\u05D7\\u05D1\\u05D9\\u05DC\\u05EA \\u05D3\\u05D5\\u05D7", ["Remind"] = "\\u05D4\\u05D6\\u05DB\\u05E8",
        ["Payment types"] = "\\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["Manage the payment types available when submitting an invoice."] = "\\u05E0\\u05D4\\u05DC \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05D6\\u05DE\\u05D9\\u05E0\\u05D9\\u05DD \\u05D1\\u05D4\\u05D2\\u05E9\\u05EA \\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA.", ["Add payment type"] = "\\u05D4\\u05D5\\u05E1\\u05E3 \\u05E1\\u05D5\\u05D2 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["Configured payment types"] = "\\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05DE\\u05D5\\u05D2\\u05D3\\u05E8\\u05D9\\u05DD", ["No payment types configured yet."] = "\\u05E2\\u05D3\\u05D9\\u05D9\\u05DF \\u05DC\\u05D0 \\u05D4\\u05D5\\u05D2\\u05D3\\u05E8\\u05D5 \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD.",
        ["Select payment type"] = "\\u05D1\\u05D7\\u05E8 \\u05E1\\u05D5\\u05D2 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD", ["No active payment types are configured. Ask an administrator to add one."] = "\\u05DC\\u05D0 \\u05D4\\u05D5\\u05D2\\u05D3\\u05E8\\u05D5 \\u05E1\\u05D5\\u05D2\\u05D9 \\u05EA\\u05E9\\u05DC\\u05D5\\u05DD \\u05E4\\u05E2\\u05D9\\u05DC\\u05D9\\u05DD.", ["Placeholder - not a real invoice"] = "\\u05DE\\u05D9\\u05D5\\u05E6\\u05D2 \\u05DC\\u05D0 \\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA \\u05D0\\u05DE\\u05D9\\u05EA\\u05D9\\u05EA", ["Placeholder"] = "\\u05DE\\u05D9\\u05D5\\u05E6\\u05D2", ["Kind"] = "\\u05E1\\u05D5\\u05D2", ["Invoice"] = "\\u05D7\\u05E9\\u05D1\\u05D5\\u05E0\\u05D9\\u05EA", ["Sent by email only"] = "\\u05E0\\u05E9\\u05DC\\u05D7 \\u05D1\\u05D3\\u05D5\\u05D0\\u05E8 \\u05D1\\u05DC\\u05D1\\u05D3"
    };

    private static readonly IReadOnlyDictionary<string, string> InventoryRussian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Inventory"] = "\u0418\u043d\u0432\u0435\u043d\u0442\u0430\u0440\u0438\u0437\u0430\u0446\u0438\u044f", ["Inventory search"] = "\u041f\u043e\u0438\u0441\u043a \u043f\u043e \u0438\u043d\u0432\u0435\u043d\u0442\u0430\u0440\u044e", ["Inventory management"] = "\u0423\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435 \u0438\u043d\u0432\u0435\u043d\u0442\u0430\u0440\u0435\u043c", ["Search parts and report what you took."] = "\u0418\u0449\u0438\u0442\u0435 \u0434\u0435\u0442\u0430\u043b\u0438 \u0438 \u043e\u0442\u043c\u0435\u0447\u0430\u0439\u0442\u0435, \u0447\u0442\u043e \u0432\u044b \u0432\u0437\u044f\u043b\u0438.", ["Search"] = "\u041f\u043e\u0438\u0441\u043a", ["Part number"] = "\u041f\u0430\u0440\u0442\u043d\u043e\u043c\u0435\u0440", ["Description"] = "\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435", ["Tags"] = "\u0422\u0435\u0433\u0438", ["Total"] = "\u0412\u0441\u0435\u0433\u043e", ["Locations and quantity"] = "\u041c\u0435\u0441\u0442\u0430 \u0438 \u043a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e", ["No quantity reported."] = "\u041a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e \u043d\u0435 \u0443\u043a\u0430\u0437\u0430\u043d\u043e.", ["I took one"] = "\u042f \u0432\u0437\u044f\u043b", ["Clear"] = "\u041e\u0447\u0438\u0441\u0442\u0438\u0442\u044c", ["No inventory items found."] = "\u041d\u0430\u0439\u0434\u0435\u043d\u043d\u044b\u0445 \u0434\u0435\u0442\u0430\u043b\u0435\u0439 \u043d\u0435\u0442.",
        ["Add parts, increase quantities and move stock between drawers."] = "\u0414\u043e\u0431\u0430\u0432\u043b\u044f\u0439\u0442\u0435 \u0434\u0435\u0442\u0430\u043b\u0438, \u0443\u0432\u0435\u043b\u0438\u0447\u0438\u0432\u0430\u0439\u0442\u0435 \u043a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e \u0438 \u043f\u0435\u0440\u0435\u043c\u0435\u0449\u0430\u0439\u0442\u0435 \u043e\u0441\u0442\u0430\u0442\u043a\u0438 \u043c\u0435\u0436\u0434\u0443 \u044f\u0449\u0438\u043a\u0430\u043c\u0438.", ["Add or increase stock"] = "\u0414\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u0438\u043b\u0438 \u0443\u0432\u0435\u043b\u0438\u0447\u0438\u0442\u044c \u043e\u0441\u0442\u0430\u0442\u043e\u043a", ["If the part number already exists, the quantity is added to the selected location."] = "\u0415\u0441\u043b\u0438 \u043f\u0430\u0440\u0442\u043d\u043e\u043c\u0435\u0440 \u0443\u0436\u0435 \u0435\u0441\u0442\u044c, \u043a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e \u0434\u043e\u0431\u0430\u0432\u043b\u044f\u0435\u0442\u0441\u044f \u0432 \u0432\u044b\u0431\u0440\u0430\u043d\u043d\u043e\u0435 \u043c\u0435\u0441\u0442\u043e.", ["Optional tags"] = "\u041d\u0435\u043e\u0431\u044f\u0437\u0430\u0442\u0435\u043b\u044c\u043d\u044b\u0435 \u0442\u0435\u0433\u0438", ["Unit cost"] = "\u0426\u0435\u043d\u0430 \u0437\u0430 \u0435\u0434\u0438\u043d\u0438\u0446\u0443", ["Drawer / location"] = "\u042f\u0449\u0438\u043a / \u043c\u0435\u0441\u0442\u043e", ["For example: Cabinet 2 / Drawer 4"] = "\u041d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: \u0448\u043a\u0430\u0444 2 / \u044f\u0449\u0438\u043a 4", ["Quantity"] = "\u041a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e", ["Save stock"] = "\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c \u043e\u0441\u0442\u0430\u0442\u043e\u043a", ["Move stock"] = "\u041f\u0435\u0440\u0435\u043c\u0435\u0449\u0435\u043d\u0438\u0435", ["Select part"] = "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0434\u0435\u0442\u0430\u043b\u044c", ["From location"] = "\u0418\u0437 \u043c\u0435\u0441\u0442\u0430", ["Select location"] = "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u043c\u0435\u0441\u0442\u043e", ["To location"] = "\u0412 \u043c\u0435\u0441\u0442\u043e", ["Existing or new drawer"] = "\u0421\u0443\u0449\u0435\u0441\u0442\u0432\u0443\u044e\u0449\u0438\u0439 \u0438\u043b\u0438 \u043d\u043e\u0432\u044b\u0439 \u044f\u0449\u0438\u043a", ["Comment"] = "\u041a\u043e\u043c\u043c\u0435\u043d\u0442\u0430\u0440\u0438\u0439", ["Move"] = "\u041f\u0435\u0440\u0435\u043c\u0435\u0441\u0442\u0438\u0442\u044c", ["Current stock"] = "\u0422\u0435\u043a\u0443\u0449\u0438\u0435 \u043e\u0441\u0442\u0430\u0442\u043a\u0438", ["Recent movements"] = "\u041f\u043e\u0441\u043b\u0435\u0434\u043d\u0438\u0435 \u043e\u043f\u0435\u0440\u0430\u0446\u0438\u0438", ["No movements yet."] = "\u041e\u043f\u0435\u0440\u0430\u0446\u0438\u0439 \u043f\u043e\u043a\u0430 \u043d\u0435\u0442.", ["Operation"] = "\u041e\u043f\u0435\u0440\u0430\u0446\u0438\u044f", ["Receipt"] = "\u041f\u0440\u0438\u0445\u043e\u0434", ["Take"] = "\u0412\u0437\u044f\u043b", ["Transfer"] = "\u041f\u0435\u0440\u0435\u043c\u0435\u0449\u0435\u043d\u0438\u0435"
    };

    private static readonly IReadOnlyDictionary<string, string> InventoryHebrew = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Inventory"] = "\\u05DE\\u05DC\\u05D0\\u05D9", ["Inventory search"] = "\\u05D7\\u05D9\\u05E4\\u05D5\\u05E9 \\u05D1\\u05DE\\u05DC\\u05D0\\u05D9", ["Inventory management"] = "\\u05E0\\u05D9\\u05D4\\u05D5\\u05DC \\u05DE\\u05DC\\u05D0\\u05D9", ["Search parts and report what you took."] = "\\u05D7\\u05E4\\u05E9 \\u05D7\\u05DC\\u05E7\\u05D9\\u05DD \\u05D5\\u05D3\\u05D5\\u05D5\\u05D7 \\u05DE\\u05D4 \\u05E9\\u05DC\\u05E7\\u05D7\\u05EA.", ["Search"] = "\\u05D7\\u05E4\\u05E9", ["Part number"] = "\\u05DE\\u05E1\\u05E4\\u05E8 \\u05D7\\u05DC\\u05E7", ["Description"] = "\\u05EA\\u05D9\\u05D0\\u05D5\\u05E8", ["Tags"] = "\\u05EA\\u05D2\\u05D9\\u05D5\\u05EA", ["Total"] = "\\u05E1\\u05D4\\u05F4\\u05DB", ["Locations and quantity"] = "\\u05DE\\u05D9\\u05E7\\u05D5\\u05DE\\u05D9\\u05DD \\u05D5\\u05DB\\u05DE\\u05D5\\u05EA", ["No quantity reported."] = "\\u05DC\\u05D0 \\u05D3\\u05D5\\u05D5\\u05D7 \\u05DE\\u05DC\\u05D0\\u05D9.", ["I took one"] = "\\u05DC\\u05E7\\u05D7\\u05EA\\u05D9 \\u05D0\\u05D7\\u05D3", ["Clear"] = "\\u05E0\\u05E7\\u05D4", ["No inventory items found."] = "\\u05DC\\u05D0 \\u05E0\\u05DE\\u05E6\\u05D0\\u05D5 \\u05E4\\u05E8\\u05D9\\u05D8\\u05D9 \\u05DE\\u05DC\\u05D0\\u05D9.",
        ["Add parts, increase quantities and move stock between drawers."] = "\\u05D4\\u05D5\\u05E1\\u05E3 \\u05D7\\u05DC\\u05E7\\u05D9\\u05DD, \\u05D4\\u05D2\\u05D3\\u05DC \\u05DB\\u05DE\\u05D5\\u05D9\\u05D5\\u05EA \\u05D5\\u05D4\\u05E2\\u05D1\\u05E8 \\u05DE\\u05DC\\u05D0\\u05D9 \\u05D1\\u05D9\\u05DF \\u05DE\\u05D2\\u05D9\\u05E8\\u05D5\\u05EA.", ["Add or increase stock"] = "\\u05D4\\u05D5\\u05E1\\u05E3 \\u05D0\\u05D5 \\u05D4\\u05D2\\u05D3\\u05DC \\u05DE\\u05DC\\u05D0\\u05D9", ["If the part number already exists, the quantity is added to the selected location."] = "\\u05D0\\u05DD \\u05DE\\u05E1\\u05E4\\u05E8 \\u05D4\\u05D7\\u05DC\\u05E7 \\u05DB\\u05D1\\u05E8 \\u05E7\\u05D9\\u05D9\\u05DD, \\u05D4\\u05DB\\u05DE\\u05D5\\u05EA \\u05EA\\u05D5\\u05E1\\u05E3 \\u05DC\\u05DE\\u05D9\\u05E7\\u05D5\\u05DD \\u05E9\\u05E0\\u05D1\\u05D7\\u05E8.", ["Optional tags"] = "\\u05EA\\u05D2\\u05D9\\u05D5\\u05EA \\u05D0\\u05D5\\u05E4\\u05E6\\u05D9\\u05D5\\u05E0\\u05DC\\u05D9\\u05D5\\u05EA", ["Unit cost"] = "\\u05E2\\u05DC\\u05D5\\u05EA \\u05DC\\u05D9\\u05D7\\u05D9\\u05D3\\u05D4", ["Drawer / location"] = "\\u05DE\\u05D2\\u05D9\\u05E8\\u05D4 / \\u05DE\\u05D9\\u05E7\\u05D5\\u05DD", ["Quantity"] = "\\u05DB\\u05DE\\u05D5\\u05EA", ["Save stock"] = "\\u05E9\\u05DE\\u05D5\\u05E8 \\u05DE\\u05DC\\u05D0\\u05D9", ["Move stock"] = "\\u05D4\\u05E2\\u05D1\\u05E8\\u05EA \\u05DE\\u05DC\\u05D0\\u05D9", ["Select part"] = "\\u05D1\\u05D7\\u05E8 \\u05D7\\u05DC\\u05E7", ["From location"] = "\\u05DE\\u05DE\\u05E7\\u05D5\\u05DD", ["Select location"] = "\\u05D1\\u05D7\\u05E8 \\u05DE\\u05D9\\u05E7\\u05D5\\u05DD", ["To location"] = "\\u05DC\\u05DE\\u05D9\\u05E7\\u05D5\\u05DD", ["Existing or new drawer"] = "\\u05DE\\u05D2\\u05D9\\u05E8\\u05D4 \\u05E7\\u05D9\\u05D9\\u05DE\\u05EA \\u05D0\\u05D5 \\u05D7\\u05D3\\u05E9\\u05D4", ["Comment"] = "\\u05D4\\u05E2\\u05E8\\u05D4", ["Move"] = "\\u05D4\\u05E2\\u05D1\\u05E8", ["Current stock"] = "\\u05DE\\u05DC\\u05D0\\u05D9 \\u05E0\\u05D5\\u05DB\\u05D7\\u05D9", ["Recent movements"] = "\\u05EA\\u05E0\\u05D5\\u05E2\\u05D5\\u05EA \\u05D0\\u05D7\\u05E8\\u05D5\\u05E0\\u05D5\\u05EA", ["No movements yet."] = "\\u05E2\\u05D3\\u05D9\\u05D9\\u05DF \\u05D0\\u05D9\\u05DF \\u05EA\\u05E0\\u05D5\\u05E2\\u05D5\\u05EA.", ["Operation"] = "\\u05E4\\u05E2\\u05D5\\u05DC\\u05D4", ["Receipt"] = "\\u05E7\\u05D1\\u05DC\\u05D4", ["Take"] = "\\u05DC\\u05E7\\u05D9\\u05D7\\u05D4", ["Transfer"] = "\\u05D4\\u05E2\\u05D1\\u05E8\\u05D4"
    };

    public string Language(IHttpContextAccessor context) => Normalize(context.HttpContext?.Request.Cookies[LanguageCookieName]);
    public string Direction(IHttpContextAccessor context) => Language(context) == "he" ? "rtl" : "ltr";
    public string Get(IHttpContextAccessor context, string value)
    {
        var language = Language(context);
        if (language == "ru" && Russian.TryGetValue(value, out var russian)) return russian;
        if (language == "ru" && RussianAdditional.TryGetValue(value, out russian)) return russian;
        if (language == "ru" && InventoryRussian.TryGetValue(value, out russian)) return russian;
        if (language != "he") return value;
        if (InventoryHebrew.TryGetValue(value, out var inventoryHebrew)) return DecodeTranslation(inventoryHebrew);
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
        if (Language(context) == "ru") return CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(month);
        return CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(month);
    }

    public string LanguageUrl(IHttpContextAccessor context, string language)
    {
        var httpContext = context.HttpContext;
        var returnUrl = httpContext is null ? "/" : $"{httpContext.Request.PathBase}{httpContext.Request.Path}{httpContext.Request.QueryString}";
        return $"/language/set?language={Uri.EscapeDataString(Normalize(language))}&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    public static string Normalize(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "he" => "he",
        "ru" => "ru",
        _ => "en"
    };
}
