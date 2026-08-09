using System.ComponentModel.DataAnnotations;
using EfcomReport.Data;
using EfcomReport.Models;
using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Pages.Admin;

public class SettingsModel(AppDbContext db, EmailService email, IConfiguration configuration, CurrentUserService currentUser) : PageModel
{
    public List<Employee> Employees { get; private set; } = [];
    public List<AppUser> Administrators { get; private set; } = [];
    public List<LeaveType> LeaveTypes { get; private set; } = [];
    public List<ReportRecipient> Recipients { get; private set; } = [];
    [BindProperty] public string? EmployeeName { get; set; }
    [BindProperty, EmailAddress] public string? EmployeeEmail { get; set; }
    [BindProperty] public string? AdministratorName { get; set; }
    [BindProperty, EmailAddress] public string? AdministratorEmail { get; set; }
    [BindProperty] public string? LeaveTypeName { get; set; }
    [BindProperty, EmailAddress] public string? RecipientEmail { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddEmployeeAsync()
    {
        var name = (EmployeeName ?? "").Trim();
        var emailAddress = (EmployeeEmail ?? "").Trim().ToLowerInvariant();
        // Validate the normalized values so pasted whitespace does not reject a valid address.
        ModelState.Remove(nameof(EmployeeName));
        ModelState.Remove(nameof(EmployeeEmail));
        if (string.IsNullOrWhiteSpace(name)) ModelState.AddModelError(nameof(EmployeeName), "Enter a name.");
        if (!new EmailAddressAttribute().IsValid(emailAddress)) ModelState.AddModelError(nameof(EmployeeEmail), "Enter a valid email.");
        if (new EmailAddressAttribute().IsValid(emailAddress) && await db.Employees.AnyAsync(x => x.Email == emailAddress)) ModelState.AddModelError(nameof(EmployeeEmail), "Employee email already exists.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var employee = new Employee { Name = name, Email = emailAddress };
        db.Employees.Add(employee);
        var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == emailAddress);
        if (user is not null) { user.Employee = employee; user.IsActive = true; }
        await db.SaveChangesAsync();

        if (!email.IsConfigured)
        {
            TempData["Message"] = $"Employee added. SMTP is not configured, so no invitation email was sent to {emailAddress}.";
            return RedirectToPage();
        }

        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        try
        {
            await email.SendAsync([emailAddress], "EfcomReport invitation", $"Hello {name},\n\nYou have been invited to use EfcomReport. Sign in with this Google account here: {publicUrl}/account/login\n\nYour administrator has added you to the leave tracker.");
            TempData["Message"] = $"Employee added and invitation sent to {emailAddress}.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Employee added, but the invitation email could not be sent: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleEmployeeAsync(int id)
    {
        var employee = await db.Employees.FindAsync(id); if (employee is null) return NotFound(); employee.IsActive = !employee.IsActive; await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddAdministratorAsync()
    {
        var name = (AdministratorName ?? "").Trim();
        var emailAddress = (AdministratorEmail ?? "").Trim().ToLowerInvariant();
        ModelState.Remove(nameof(AdministratorName));
        ModelState.Remove(nameof(AdministratorEmail));
        if (!new EmailAddressAttribute().IsValid(emailAddress)) ModelState.AddModelError(nameof(AdministratorEmail), "Enter a valid email.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        var user = await db.AppUsers.SingleOrDefaultAsync(x => x.Email == emailAddress);
        if (user is null)
        {
            user = new AppUser { Email = emailAddress, DisplayName = string.IsNullOrWhiteSpace(name) ? emailAddress : name, Role = "Admin" };
            db.AppUsers.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name)) user.DisplayName = name;
            user.Role = "Admin";
            user.IsActive = true;
        }
        await db.SaveChangesAsync();

        if (!email.IsConfigured)
        {
            TempData["Message"] = "Administrator added. SMTP is not configured, so no invitation email was sent.";
            return RedirectToPage();
        }

        var publicUrl = (configuration["App:PublicUrl"] ?? "http://localhost:5186").TrimEnd('/');
        try
        {
            await email.SendAsync([emailAddress], "EfcomReport administrator invitation", $"Hello {name},\n\nYou have been invited as an administrator for EfcomReport.\n\nSign in here: {publicUrl}/account/login");
            TempData["Message"] = $"Administrator added and invitation sent to {emailAddress}.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Administrator added, but the invitation email could not be sent: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAdministratorAsync(int id)
    {
        var admin = await db.AppUsers.SingleOrDefaultAsync(x => x.Id == id && x.Role == "Admin");
        if (admin is null) return NotFound();
        var currentEmail = currentUser.Email(User);
        if (string.Equals(admin.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Message"] = "You cannot deactivate your own administrator account.";
            return RedirectToPage();
        }
        admin.IsActive = !admin.IsActive;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddLeaveTypeAsync()
    {
        var name = (LeaveTypeName ?? "").Trim();
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
        var email = (RecipientEmail ?? "").Trim().ToLowerInvariant();
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
        Administrators = await db.AppUsers.Where(x => x.Role == "Admin").OrderBy(x => x.Email).ToListAsync();
        LeaveTypes = await db.LeaveTypes.OrderBy(x => x.Id).ToListAsync();
        Recipients = await db.ReportRecipients.OrderBy(x => x.Email).ToListAsync();
    }
}
