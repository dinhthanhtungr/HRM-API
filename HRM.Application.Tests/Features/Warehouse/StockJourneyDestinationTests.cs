using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using Xunit;
using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockJourneyDestinationTests
{
    [WarehouseReadOnlyFact]
    public async Task ActualPxkVouchersSuppressMissingDestinationWarningInJourney()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "JourneyVoucherDestinationReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var voucher = await db.WarehouseVouchers.AsNoTracking().SingleAsync(x => x.VoucherCode == "PXK261100541");
        var seed = await db.WarehouseMovementRows.AsNoTracking().SingleAsync(x => x.CompanyId == voucher.CompanyId
            && x.VoucherId == voucher.VoucherId && x.ProductCode == "NVL_NH_733");
        var user = new User(voucher.CompanyId);
        var permissions = new CurrentUserPermissionService(user);
        var output = (await new GetStockJourneyQueryHandler(db, user, permissions, new WarehouseVoucherBusinessReader(db, user, permissions))
            .Handle(new(seed.MovementKey, seed.LedgerId), default)).Data!;
        foreach (var code in new[] { "PXK261100521", "PXK261100541" })
        {
            var issue = Assert.Single(output.Events.Items, x => x.Movement.ReferenceCodes.Contains(code));
            Assert.Equal("VerifiedVoucherIssue", issue.DestinationStatus);
            Assert.False(issue.ShowMissingDestinationWarning);
            Assert.Empty(issue.Movement.ToShelves);
        }
        var receipt = Assert.Single(output.Events.Items, x => x.Movement.ReferenceCodes.Contains("PNK261100877"));
        Assert.Equal("NotApplicable", receipt.DestinationStatus);
        Assert.False(receipt.ShowMissingDestinationWarning);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("valid", "VerifiedVoucherIssue")]
    [InlineData("foreign", "Unresolved")]
    [InlineData("wrongVoucher", "Unresolved")]
    [InlineData("wrongDetail", "Unresolved")]
    [InlineData("wrongProduct", "Unresolved")]
    [InlineData("wrongLot", "Unresolved")]
    [InlineData("receipt", "Unresolved")]
    [InlineData("pending", "Unresolved")]
    [InlineData("missing", "Unresolved")]
    public async Task VoucherIssueIsVerifiedByCompanyDetailIdentityDirectionAndAppliedStatus(string scenario, string expected)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var row = new WarehouseMovementRow { CompanyId = company, LedgerId = 1, MovementKey = "issue",
            ProductCode = "ITEM", LotNumber = "LOT", DeltaKg = -100, VoucherId = 2, VoucherDetailId = 3 };
        var voucher = new WarehouseVoucher { CompanyId = company, VoucherId = 2, VoucherCode = "PXK-1", Status = "APPLIED" };
        var detail = new WarehouseVoucherDetail { VoucherId = 2, VoucherDetailId = 3, ProductCode = "ITEM", LotNumber = "LOT", IsIncrease = false };
        switch (scenario)
        {
            case "foreign": voucher.CompanyId = Guid.NewGuid(); break;
            case "wrongVoucher": row.VoucherId = 9; break;
            case "wrongDetail": row.VoucherDetailId = 9; break;
            case "wrongProduct": detail.ProductCode = "OTHER"; break;
            case "wrongLot": detail.LotNumber = "OTHER"; break;
            case "receipt": detail.IsIncrease = true; break;
            case "pending": voucher.Status = "PENDING"; break;
            case "missing": row.VoucherDetailId = null; break;
        }
        store.Seed(nameof(db.WarehouseVouchers), voucher);
        store.Seed(nameof(db.WarehouseVoucherDetails), detail);
        var verified = await StockJourneyDestinationReader.VerifiedVoucherIssuesAsync(db, company, [row], default);
        var movement = StockMovementReader.Summarize([row]);
        var status = StockJourneyDestinationReader.Status(movement, [row], verified);
        Assert.Equal(expected, status);
        var output = new HRM.Application.Features.Warehouse.Dtos.StockJourneyEventDto(movement, []) { DestinationStatus = status };
        Assert.Equal(expected == "Unresolved", output.ShowMissingDestinationWarning);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void InternalDestinationAndReceiptDoNotRequireExternalUsageLink()
    {
        var rows = new[] { new WarehouseMovementRow { DeltaKg = -100, ShelfStockCode = "A" },
            new WarehouseMovementRow { DeltaKg = 100, ShelfStockCode = "B" } };
        Assert.Equal("InternalDestination", StockJourneyDestinationReader.Status(StockMovementReader.Summarize(rows), rows, new HashSet<long>()));
        Assert.Equal("NotApplicable", StockJourneyDestinationReader.Status(StockMovementReader.Summarize([rows[1]]), [rows[1]], new HashSet<long>()));
    }

    private sealed class User(Guid company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
