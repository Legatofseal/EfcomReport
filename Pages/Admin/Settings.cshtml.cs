using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class SettingsModel(AppDbContext db) : PageModel
{
    public List<Employee> Employees { get; private set; } = [];
    public List<LeaveType> LeaveTypes { get; private set; } = [];
    public List<ReportRecipient> Recipients { get; private set; } = [];
    [BindProperty] public string EmployeeName { get; set; } = "";
    [BindProperty, EmailAddress] public string EmployeeEmail { get; set; } = "";
    [BindProperty] public string LeaveTypeName { get; set; } = "";
    [BindProperty, EmailAddress] public string RecipientEmail { get; set; } = "";

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(EmployeeName) || !new EmailAddressAttribute().IsValid(EmployeeEmail)) ModelState.AddModelError(string.Empty, "Enter a name and valid email.");
        if (await db.Employees.AnyAsync(x => x.Email == EmployeeEmail.Trim().ToLowerInvariant())) ModelState.AddModelError(string.Empty, "Employee email already exists.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var email = EmployeeEmail.Trim().ToLowerInvariant();
        var employee = new Employee { Name = EmployeeName.Trim(), Email = email };
        db.Employees.Add(employee);
        var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
        if (user is not null) user.Employee = employee;
        await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleEmployeeAsync(int id)
    {
        var employee = await db.Employees.FindAsync(id); if (employee is null) return NotFound(); employee.IsActive = !employee.IsActive; await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddLeaveTypeAsync()
    {
        var name = LeaveTypeName.Trim();
        if (string.IsNullOrWhiteSpace(name)) ModelState.AddModelError(string.Empty, "Enter a leave type.");
        if (await db.LeaveTypes.AnyAsync(x => x.Name == name)) ModelState.AddModelError(string.Empty, "Leave type already exists.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        db.LeaveTypes.Add(new LeaveType { Name = name }); await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleLeaveTypeAsync(int id)
    {
        var type = await db.LeaveTypes.FindAsync(id); if (type is null) return NotFound(); type.IsActive = !type.IsActive; await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddRecipientAsync()
    {
        var email = RecipientEmail.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(email)) ModelState.AddModelError(string.Empty, "Enter a valid recipient email.");
        if (await db.ReportRecipients.AnyAsync(x => x.Email == email)) ModelState.AddModelError(string.Empty, "Recipient already exists.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        db.ReportRecipients.Add(new ReportRecipient { Email = email }); await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleRecipientAsync(int id)
    {
        var recipient = await db.ReportRecipients.FindAsync(id); if (recipient is null) return NotFound(); recipient.IsActive = !recipient.IsActive; await db.SaveChangesAsync(); return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Employees = await db.Employees.OrderBy(x => x.Name).ToListAsync();
        LeaveTypes = await db.LeaveTypes.OrderBy(x => x.Id).ToListAsync();
        Recipients = await db.ReportRecipients.OrderBy(x => x.Email).ToListAsync();
    }
}
