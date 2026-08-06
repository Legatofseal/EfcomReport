namespace EfcomReport.Models;

public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AbsenceRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByEmail { get; set; } = "";
    public bool IsCancelled { get; set; }
    public string? CancelledByEmail { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}

public sealed class MonthlySubmission
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public bool HasAbsence { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WorkdayOverride
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool IsWorking { get; set; }
    public string? Note { get; set; }
    public string UpdatedByEmail { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportRecipient
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class ReminderRun
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string RequestedByEmail { get; set; } = "";
    public string SentToEmail { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
}
