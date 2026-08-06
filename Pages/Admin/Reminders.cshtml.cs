using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EfcomReport.Pages.Admin;

public class RemindersModel(ReminderService reminders) : PageModel
{
    public string? Result { get; private set; }
    public async Task<IActionResult> OnPostRunNowAsync()
    {
        try { Result = $"Reminder run complete. Sent: {await reminders.RunAsync(true)}."; }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        return Page();
    }
}
