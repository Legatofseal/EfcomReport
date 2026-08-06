using EfcomReport.Models;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();
    public DbSet<MonthlySubmission> MonthlySubmissions => Set<MonthlySubmission>();
    public DbSet<WorkdayOverride> WorkdayOverrides => Set<WorkdayOverride>();
    public DbSet<ReportRecipient> ReportRecipients => Set<ReportRecipient>();
    public DbSet<ReminderRun> ReminderRuns => Set<ReminderRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<LeaveType>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<MonthlySubmission>().HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
        modelBuilder.Entity<WorkdayOverride>().HasIndex(x => x.Date).IsUnique();
        modelBuilder.Entity<ReportRecipient>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<ReminderRun>().HasIndex(x => new { x.Year, x.Month }).IsUnique();

        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, Name = "Vacation", IsActive = true },
            new LeaveType { Id = 2, Name = "Miluim", IsActive = true },
            new LeaveType { Id = 3, Name = "Sick Leave", IsActive = true });
    }
}
