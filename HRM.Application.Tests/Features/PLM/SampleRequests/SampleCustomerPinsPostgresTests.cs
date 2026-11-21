using HRM.Application.Features.PLM.SampleRequests.PersonalWork;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleCustomerPinsPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task BulkPinsAcceptLocalClockAndPreserveExistingReminderOnConflict()
    {
        await using var connection = await postgres.OpenAsync();
        await using (var schema = new NpgsqlCommand("""
            CREATE SCHEMA IF NOT EXISTS "SampleRequests";
            CREATE TABLE "SampleRequests"."SampleRequestUserPreferences" (
              "Id" uuid PRIMARY KEY, "CompanyId" uuid NOT NULL, "EmployeeId" uuid NOT NULL,
              "SampleRequestId" uuid NOT NULL, "IsPinned" boolean NOT NULL, "NotifyOnDue" boolean NOT NULL,
              "PinnedAt" timestamp without time zone, "CreatedDate" timestamp without time zone NOT NULL,
              "UpdatedDate" timestamp without time zone,
              UNIQUE ("CompanyId", "EmployeeId", "SampleRequestId"));
            """, connection)) await schema.ExecuteNonQueryAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var company = Guid.NewGuid(); var employee = Guid.NewGuid();
        var now = new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Local);
        await db.Database.ExecuteSqlInterpolatedAsync(SaveSampleCustomerPinsCommandHandler.Upsert(ids, company, employee, true, now));
        await using (var reminder = new NpgsqlCommand("""UPDATE "SampleRequests"."SampleRequestUserPreferences" SET "NotifyOnDue"=true""", connection))
            await reminder.ExecuteNonQueryAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(SaveSampleCustomerPinsCommandHandler.Upsert(ids, company, employee, false, now.AddMinutes(1)));
        await using var read = new NpgsqlCommand("""SELECT count(*) FROM "SampleRequests"."SampleRequestUserPreferences" WHERE NOT "IsPinned" AND "PinnedAt" IS NULL AND "NotifyOnDue" AND "CreatedDate"=timestamp '2026-11-10 15:00:00'""", connection);
        Assert.Equal(2L, await read.ExecuteScalarAsync());
    }
}
