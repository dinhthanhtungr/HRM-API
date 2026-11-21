using System.Data.Common;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Application.Features.Warehouse.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockJourneyProfileFactAttribute : FactAttribute
{
    public StockJourneyProfileFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HRM_JOURNEY_PROFILE_KEY")) ||
            !long.TryParse(Environment.GetEnvironmentVariable("HRM_JOURNEY_PROFILE_LEDGER"), out _))
            Skip = "Opt-in read-only journey profiling; requires connection settings and an existing movement/ledger.";
    }
}

public sealed class StockJourneyReadOnlyProfileTests(ITestOutputHelper output)
{
    [StockJourneyProfileFact]
    public async Task ProfileExistingJourneyWithoutWritingOrLoggingParameters()
    {
        var key = Environment.GetEnvironmentVariable("HRM_JOURNEY_PROFILE_KEY");
        if (string.IsNullOrEmpty(key) || !long.TryParse(Environment.GetEnvironmentVariable("HRM_JOURNEY_PROFILE_LEDGER"), out var ledger))
            return;
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var s = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = s.GetProperty("host").GetString(), Port = s.GetProperty("port").GetInt32(),
            Database = s.GetProperty("database").GetString(), Username = s.GetProperty("user").GetString(),
            Password = s.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "StockJourneyReadOnlyProfile"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var capture = new Capture();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(capture).Options);
        await db.Database.UseTransactionAsync(transaction);
        var seed = await db.WarehouseMovementRows.AsNoTracking()
            .Where(x => x.MovementKey == key && x.LedgerId == ledger)
            .Select(x => new { x.CompanyId, x.ProductCode, x.LotNumber }).SingleAsync();
        foreach (var mixing in new[] { true, false })
        {
            var user = new User(seed.CompanyId, mixing);
            var permissions = new CurrentUserPermissionService(user);
            var handler = new GetStockJourneyQueryHandler(db, user, permissions, new HRM.Application.Features.Warehouse.Services.WarehouseVoucherBusinessReader(db, user, permissions));
            var referenceRows = StockMovementReader.VisibleRows(db, user, permissions);
            if (!await referenceRows.AnyAsync(x => x.MovementKey == key && x.LedgerId == ledger))
            {
                capture.Commands.Clear();
                var denied = await handler.Handle(new(key, ledger), default);
                Assert.False(denied.Success);
                Assert.Null(denied.Data);
                Assert.Single(capture.Commands);
                output.WriteLine($"mixing={mixing} seedDenied=True queries=1");
                continue;
            }
            // Compare to the original grouping semantics against the same read-only data.
            var expected = await referenceRows
                .Where(x => x.ProductCode == seed.ProductCode && x.LotNumber == seed.LotNumber)
                .GroupBy(x => x.MovementKey)
                .Select(g => new { Key = g.Key, At = g.Min(x => x.MovementAt), FirstId = g.Min(x => x.LedgerId) })
                .OrderBy(x => x.At).ThenBy(x => x.FirstId).ToArrayAsync();
            for (var iteration = 0; iteration < 3; iteration++)
            {
                capture.Commands.Clear();
                var timer = Stopwatch.StartNew();
                var result = await handler.Handle(new(key, ledger), default);
                timer.Stop();
                Assert.True(result.Success);
                Assert.Equal(expected.Length, result.Data!.Events.TotalCount);
                Assert.Equal(expected.Take(40).Select(x => x.Key), result.Data.Events.Items.Select(x => x.Movement.MovementKey));
                Assert.Equal(expected.FirstOrDefault()?.At, result.Data.FirstRecordedAt);
                Assert.Equal(expected.LastOrDefault()?.At, result.Data.LastRecordedAt);
                Assert.Equal(8, capture.Commands.Count);
                output.WriteLine($"mixing={mixing} run={iteration + 1} success={result.Success} totalMs={timer.Elapsed.TotalMilliseconds:F1} queries={capture.Commands.Count}");
                for (var i = 0; i < capture.Commands.Count; i++)
                    output.WriteLine($"  query={i + 1} ms={capture.Commands[i].ElapsedMs:F1}");
                if (iteration != 2) continue;
                for (var commandIndex = 0; commandIndex < capture.Commands.Count; commandIndex++)
                {
                    var command = capture.Commands[commandIndex];
                    // Only EF-generated SELECT text is explained; all values stay bound parameters.
                    Assert.StartsWith("SELECT", command.Sql.TrimStart(), StringComparison.OrdinalIgnoreCase);
                    await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + command.Sql, connection);
                    explain.Parameters.AddRange(command.Parameters.Select(p => (NpgsqlParameter)((ICloneable)p).Clone()).ToArray());
                    using var plan = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);
                    var summary = plan.RootElement[0];
                    var description = Describe(summary.GetProperty("Plan"));
                    if (commandIndex is 1 or 2) Assert.DoesNotContain("WindowAgg", description);
                    output.WriteLine($"  planMs={summary.GetProperty("Execution Time").GetDouble():F1} " + description);
                }
            }
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static string Describe(JsonElement node)
    {
        var text = node.GetProperty("Node Type").GetString()!;
        if (node.TryGetProperty("Relation Name", out var relation)) text += ":" + relation.GetString();
        if (node.TryGetProperty("Actual Rows", out var rows)) text += " rows=" + rows.GetInt64();
        if (node.TryGetProperty("Rows Removed by Filter", out var removed)) text += " filtered=" + removed.GetInt64();
        if (node.TryGetProperty("Plans", out var children)) text += " [" + string.Join("; ", children.EnumerateArray().Select(Describe)) + "]";
        return text;
    }

    private sealed record MeasuredCommand(string Sql, NpgsqlParameter[] Parameters, double ElapsedMs);
    private sealed class Capture : DbCommandInterceptor
    {
        public List<MeasuredCommand> Commands { get; } = [];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(p => (NpgsqlParameter)((ICloneable)p).Clone()).ToArray(), eventData.Duration.TotalMilliseconds));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class User(Guid company, bool mixing) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Production.PLPUUser];
        public IReadOnlyCollection<string> Permissions => mixing ? [ApplicationPermissions.Warehouse.ViewMixingStock] : [];
        public bool HasExplicitPermissionSet => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
