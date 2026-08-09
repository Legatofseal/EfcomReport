using System.Data;
using EfcomReport.Data;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Services;

public static class PortalDatabaseInitializer
{
    public static void EnsureCreated(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        if (!db.Database.IsSqlite()) return;

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ReportRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ReportRequests" PRIMARY KEY AUTOINCREMENT,
                "EmployeeId" INTEGER NOT NULL,
                "Year" INTEGER NOT NULL,
                "Month" INTEGER NOT NULL,
                "RequestedByEmail" TEXT NOT NULL,
                "SentToEmail" TEXT NOT NULL,
                "RequestedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_ReportRequests_Employees_EmployeeId"
                    FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_ReportRequests_EmployeeId_Year_Month"
            ON "ReportRequests" ("EmployeeId", "Year", "Month");
            CREATE TABLE IF NOT EXISTS "InvoiceEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceEntries" PRIMARY KEY AUTOINCREMENT,
                "SubmittedByEmail" TEXT NOT NULL,
                "RecipientEmail" TEXT NOT NULL,
                "Customer" TEXT NOT NULL,
                "InvoiceNumber" TEXT NOT NULL,
                "CurrencySymbol" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "PaymentType" TEXT NOT NULL,
                "Comments" TEXT NULL,
                "AttachmentOriginalName" TEXT NULL,
                "AttachmentStorageName" TEXT NULL,
                "AttachmentContentType" TEXT NULL,
                "AttachmentSize" INTEGER NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "EmailSentAtUtc" TEXT NULL,
                "EmailError" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_InvoiceEntries_CreatedAtUtc"
            ON "InvoiceEntries" ("CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_InvoiceEntries_InvoiceNumber"
            ON "InvoiceEntries" ("InvoiceNumber");
            """);

        EnsureSqliteColumn(db, "AttachmentOriginalName", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentStorageName", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentContentType", "TEXT NULL");
        EnsureSqliteColumn(db, "AttachmentSize", "INTEGER NULL");
    }

    private static void EnsureSqliteColumn(AppDbContext db, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) db.Database.OpenConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('AbsenceRequests') WHERE name = '{column}'";
            var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"AbsenceRequests\" ADD COLUMN \"{column}\" {definition}";
                alter.ExecuteNonQuery();
            }
        }
        finally
        {
            if (!wasOpen) db.Database.CloseConnection();
        }
    }
}
