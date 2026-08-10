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
    public DbSet<InvoiceRecipient> InvoiceRecipients => Set<InvoiceRecipient>();
    public DbSet<InvoiceCustomerOption> InvoiceCustomerOptions => Set<InvoiceCustomerOption>();
    public DbSet<PaymentTypeOption> PaymentTypeOptions => Set<PaymentTypeOption>();
    public DbSet<ReminderRun> ReminderRuns => Set<ReminderRun>();
    public DbSet<ReportRequest> ReportRequests => Set<ReportRequest>();
    public DbSet<InvoiceEntry> InvoiceEntries => Set<InvoiceEntry>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<LeaveType>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<MonthlySubmission>().HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
        modelBuilder.Entity<WorkdayOverride>().HasIndex(x => x.Date).IsUnique();
        modelBuilder.Entity<ReportRecipient>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<InvoiceRecipient>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<InvoiceCustomerOption>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<PaymentTypeOption>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<ReminderRun>().HasIndex(x => new { x.Year, x.Month }).IsUnique();
        modelBuilder.Entity<ReportRequest>().HasIndex(x => new { x.EmployeeId, x.Year, x.Month });
        modelBuilder.Entity<InvoiceEntry>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<InvoiceEntry>().HasIndex(x => x.InvoiceNumber);
        modelBuilder.Entity<InventoryItem>().Property(x => x.PartNumber).UseCollation("NOCASE");
        modelBuilder.Entity<InventoryLocation>().Property(x => x.Name).UseCollation("NOCASE");
        modelBuilder.Entity<InventoryItem>().HasIndex(x => x.PartNumber).IsUnique();
        modelBuilder.Entity<InventoryLocation>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<InventoryStock>().HasIndex(x => new { x.ItemId, x.LocationId }).IsUnique();
        modelBuilder.Entity<InventoryMovement>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<InventoryMovement>()
            .HasOne(x => x.FromLocation)
            .WithMany()
            .HasForeignKey(x => x.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryMovement>()
            .HasOne(x => x.ToLocation)
            .WithMany()
            .HasForeignKey(x => x.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, Name = "Vacation", IsActive = true },
            new LeaveType { Id = 2, Name = "Miluim", IsActive = true },
            new LeaveType { Id = 3, Name = "Sick Leave", IsActive = true });
    }
}
