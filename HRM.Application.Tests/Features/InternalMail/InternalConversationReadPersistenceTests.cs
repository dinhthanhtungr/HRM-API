using System.Data.Common;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationReadPersistenceTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Upsert_UsesReadAtColumnTypeAndPreservesClockTime(DateTimeKind kind)
    {
        var capture = new CaptureReadCommand();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .AddInterceptors(new SuppressConnection(), capture).Options);
        var readAt = new DateTime(2026, 9, 30, 23, 15, 0, kind);
        await db.UpsertInternalMessageReadStatesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], readAt);

        Assert.Equal("timestamp without time zone", db.Model.FindEntityType(typeof(InternalMessageReadState))!
            .FindProperty(nameof(InternalMessageReadState.ReadAt))!.GetColumnType());
        Assert.Equal(NpgsqlDbType.Timestamp, capture.TimestampType);
        Assert.Equal(readAt.Ticks, capture.TimestampValue.Ticks);
        Assert.Equal(kind, capture.TimestampValue.Kind);
    }

    // Opt-in real-provider regression. Random scope cannot match application records;
    // rollback leaves no fixtures, tables or schema changes behind.
    [NotificationPostgresTheory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Upsert_PostgresSerializesTimestampWithoutChangingAnyRows(DateTimeKind kind)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("HRM_NOTIFICATION_TEST_CONNECTION")!).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var affected = await db.UpsertInternalMessageReadStatesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()],
            new DateTime(2026, 9, 30, 23, 15, 0, kind));
        Assert.Equal(0, affected);
        await transaction.RollbackAsync();
    }

    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class CaptureReadCommand : DbCommandInterceptor
    {
        public NpgsqlDbType TimestampType { get; private set; }
        public DateTime TimestampValue { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var parameter = Assert.Single(command.Parameters.Cast<NpgsqlParameter>(), item => item.Value is DateTime);
            TimestampType = parameter.NpgsqlDbType;
            TimestampValue = (DateTime)parameter.Value!;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }
    }
}

public sealed class NotificationPostgresTheoryAttribute : TheoryAttribute
{
    public NotificationPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRM_NOTIFICATION_TEST_CONNECTION")))
            Skip = "Set HRM_NOTIFICATION_TEST_CONNECTION to run the no-row PostgreSQL persistence regression.";
    }
}
