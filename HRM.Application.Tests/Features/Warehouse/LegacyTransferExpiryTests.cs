using System.Text.Json;
using HRM.Application.Features.Warehouse.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class LegacyTransferExpiryTests
{
    [Fact]
    public void MissingExpiryGuardStillRejectsTwoDifferentKnownDates()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var sql = db.ReadMovementHistory(Guid.NewGuid()).ToQueryString();
        Assert.Contains("a.\"ExpiryDate\" IS NOT DISTINCT FROM b.\"ExpiryDate\" OR a.\"ExpiryDate\" IS NULL OR b.\"ExpiryDate\" IS NULL", sql);
        Assert.Contains("a.xmin = b.xmin", sql);
        Assert.Contains("a.\"requestCode\" = b.\"requestCode\"", sql);
        Assert.Contains("b.\"deltaKg\" = -a.\"deltaKg\"", sql);
    }

    [WarehouseReadOnlyFact]
    public async Task ActualWmvPairWithMissingDestinationExpiryIsOneTransfer()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "LegacyTransferExpiryReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var seed = await db.WarehouseMovementRows.AsNoTracking().SingleAsync(x => x.LedgerId == 61336);
        var rows = await db.ReadMovementHistory(seed.CompanyId).Where(x => x.LedgerId == 61336 || x.LedgerId == 61337).ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.Single(rows.Select(x => x.MovementKey).Distinct());
        var item = StockMovementReader.Summarize(rows);
        Assert.Equal("InternalTransfer", item.BusinessType.Code);
        Assert.Equal(500m, item.InKg);
        Assert.Equal(500m, item.OutKg);
        Assert.Equal(0m, item.NetKg);
        Assert.Equal("W.0.1", Assert.Single(item.FromShelves));
        Assert.Equal("B.3.6", Assert.Single(item.ToShelves));
        var links = await StockMovementReader.ResolveLegacyKeyAsync(db.ReadMovementHistory(seed.CompanyId), "LEGACY_LEDGER:61337", default);
        Assert.Equal(item.MovementKey, links);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
