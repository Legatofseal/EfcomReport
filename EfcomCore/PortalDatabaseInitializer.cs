using System.Data;
using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.Extensions.Configuration;
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
                "Provider" TEXT NOT NULL,
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
            CREATE TABLE IF NOT EXISTS "InventoryItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InventoryItems" PRIMARY KEY AUTOINCREMENT,
                "PartNumber" TEXT NOT NULL COLLATE NOCASE,
                "Description" TEXT NOT NULL,
                "Tags" TEXT NOT NULL,
                "Keywords" TEXT NOT NULL,
                "UnitCost" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "CreatedByEmail" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryItems_PartNumber"
            ON "InventoryItems" ("PartNumber" COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS "InventoryLocations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InventoryLocations" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL COLLATE NOCASE,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "CreatedByEmail" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryLocations_Name"
            ON "InventoryLocations" ("Name" COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS "InventoryStocks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InventoryStocks" PRIMARY KEY AUTOINCREMENT,
                "ItemId" INTEGER NOT NULL,
                "LocationId" INTEGER NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_InventoryStocks_InventoryItems_ItemId"
                    FOREIGN KEY ("ItemId") REFERENCES "InventoryItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_InventoryStocks_InventoryLocations_LocationId"
                    FOREIGN KEY ("LocationId") REFERENCES "InventoryLocations" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryStocks_ItemId_LocationId"
            ON "InventoryStocks" ("ItemId", "LocationId");
            CREATE TABLE IF NOT EXISTS "InventoryMovements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InventoryMovements" PRIMARY KEY AUTOINCREMENT,
                "ItemId" INTEGER NOT NULL,
                "FromLocationId" INTEGER NULL,
                "ToLocationId" INTEGER NULL,
                "Quantity" INTEGER NOT NULL,
                "MovementType" TEXT NOT NULL,
                "PerformedByEmail" TEXT NOT NULL,
                "Note" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_InventoryMovements_InventoryItems_ItemId"
                    FOREIGN KEY ("ItemId") REFERENCES "InventoryItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_InventoryMovements_FromLocation"
                    FOREIGN KEY ("FromLocationId") REFERENCES "InventoryLocations" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_InventoryMovements_ToLocation"
                    FOREIGN KEY ("ToLocationId") REFERENCES "InventoryLocations" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_InventoryMovements_CreatedAtUtc"
            ON "InventoryMovements" ("CreatedAtUtc");
            CREATE TABLE IF NOT EXISTS "InvoiceRecipients" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceRecipients" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDefault" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceRecipients_Email"
            ON "InvoiceRecipients" ("Email");
            CREATE TABLE IF NOT EXISTS "InvoiceCustomerOptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceCustomerOptions" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceCustomerOptions_Name"
            ON "InvoiceCustomerOptions" ("Name");
            CREATE TABLE IF NOT EXISTS "PaymentTypeOptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PaymentTypeOptions" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentTypeOptions_Name"
            ON "PaymentTypeOptions" ("Name");
            """);

        var configuredDefault = (scope.ServiceProvider.GetRequiredService<IConfiguration>()[
            "Invoice:DefaultRecipientEmail"] ?? "").Trim().ToLowerInvariant();
        if (new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(configuredDefault) &&
            !db.InvoiceRecipients.Any())
        {
            db.InvoiceRecipients.Add(new InvoiceRecipient
            {
                Name = configuredDefault,
                Email = configuredDefault,
                IsActive = true,
                IsDefault = true
            });
            db.SaveChanges();
        }

        if (!db.InvoiceCustomerOptions.Any())
        {
            var existingCustomers = db.InvoiceEntries
                .Select(x => x.Customer)
                .Where(x => x != null && x != "")
                .Distinct()
                .ToList();
            if (existingCustomers.Count > 0)
            {
                db.InvoiceCustomerOptions.AddRange(existingCustomers.Select(name => new InvoiceCustomerOption
                {
                    Name = name,
                    IsActive = true
                }));
                db.SaveChanges();
            }
        }

        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentOriginalName", "TEXT NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentStorageName", "TEXT NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentContentType", "TEXT NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentSize", "INTEGER NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "IsHalfDay", "INTEGER NOT NULL DEFAULT 0");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentUploadedByName", "TEXT NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentUploadedByEmail", "TEXT NULL");
        EnsureSqliteColumn(db, "AbsenceRequests", "AttachmentUploadedAtUtc", "TEXT NULL");
        EnsureSqliteColumn(db, "MonthlySubmissions", "IsConfirmed", "INTEGER NOT NULL DEFAULT 0");
        EnsureSqliteColumn(db, "MonthlySubmissions", "ConfirmedAtUtc", "TEXT NULL");
        EnsureSqliteColumn(db, "InvoiceEntries", "IsPlaceholder", "INTEGER NOT NULL DEFAULT 0");
        EnsureSqliteColumn(db, "InvoiceEntries", "Provider", "TEXT NOT NULL DEFAULT ''");
        EnsureSqliteColumn(db, "InventoryItems", "Keywords", "TEXT NOT NULL DEFAULT ''");
        EnsureSqliteColumn(db, "WorkdayOverrides", "IsHalfDay", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void EnsureSqliteColumn(AppDbContext db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) db.Database.OpenConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
                alter.ExecuteNonQuery();
            }
        }
        finally
        {
            if (!wasOpen) db.Database.CloseConnection();
        }
    }
}
