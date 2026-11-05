using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261102120000_AddCommunicationDeliveryLocking")]
public sealed class AddCommunicationDeliveryLocking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // DbInitializer added these fields in earlier releases before this migration
        // existed. Guard every operation so partially upgraded databases can record
        // this migration instead of crash-looping at startup.
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.communication_deliveries', N'attempt_count') IS NULL
                ALTER TABLE [dbo].[communication_deliveries] ADD [attempt_count] INT NOT NULL CONSTRAINT [DF_communication_deliveries_attempt_count] DEFAULT 0;

            IF COL_LENGTH(N'dbo.communication_deliveries', N'next_attempt_at') IS NULL
                ALTER TABLE [dbo].[communication_deliveries] ADD [next_attempt_at] DATETIME2 NULL;

            IF COL_LENGTH(N'dbo.communication_deliveries', N'lease_until') IS NULL
                ALTER TABLE [dbo].[communication_deliveries] ADD [lease_until] DATETIME2 NULL;

            IF COL_LENGTH(N'dbo.communication_deliveries', N'locked_by') IS NULL
                ALTER TABLE [dbo].[communication_deliveries] ADD [locked_by] NVARCHAR(100) NULL;

            IF COL_LENGTH(N'dbo.communication_deliveries', N'last_error') IS NULL
                ALTER TABLE [dbo].[communication_deliveries] ADD [last_error] NVARCHAR(1000) NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[communication_deliveries]')
                  AND name = N'IX_communication_deliveries_status_next_attempt_at_lease_until')
                CREATE INDEX [IX_communication_deliveries_status_next_attempt_at_lease_until]
                    ON [dbo].[communication_deliveries] ([status], [next_attempt_at], [lease_until]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[communication_deliveries]')
                  AND name = N'IX_communication_deliveries_status_next_attempt_at_lease_until')
                DROP INDEX [IX_communication_deliveries_status_next_attempt_at_lease_until] ON [dbo].[communication_deliveries];

            IF COL_LENGTH(N'dbo.communication_deliveries', N'attempt_count') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP CONSTRAINT [DF_communication_deliveries_attempt_count];
            IF COL_LENGTH(N'dbo.communication_deliveries', N'attempt_count') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP COLUMN [attempt_count];
            IF COL_LENGTH(N'dbo.communication_deliveries', N'next_attempt_at') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP COLUMN [next_attempt_at];
            IF COL_LENGTH(N'dbo.communication_deliveries', N'lease_until') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP COLUMN [lease_until];
            IF COL_LENGTH(N'dbo.communication_deliveries', N'locked_by') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP COLUMN [locked_by];
            IF COL_LENGTH(N'dbo.communication_deliveries', N'last_error') IS NOT NULL
                ALTER TABLE [dbo].[communication_deliveries] DROP COLUMN [last_error];
            """);
    }
}

