using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryPlanQueryTests
{
    private static readonly IQueryable<Employee> NoEmployees = Array.Empty<Employee>().AsQueryable();
    private readonly Guid company = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 10, 6);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IncludeAllOrders_KeepsMissingPromisePastAndFutureRegardlessOfOverdue(bool includeOverdue)
    {
        var rows = new[] { Source(Date, 3), Source(Date.AddDays(-1), 0),
            Source(Date.AddDays(1), 3), new DeliveryPlanSource() }.AsQueryable();

        Assert.Equal(rows.ToArray(), GetDeliveryPlanQueryHandler.ForDate(rows, Date, includeOverdue, true).ToArray());
        Assert.False(new GetDeliveryPlanQuery().IncludeAllOrders);
    }

    [Fact]
    public void MissingPromise_MapsToNullWithoutInventingDateOrOverdue()
    {
        var row = new DeliveryPlanSource { Status = "Processing" };
        var result = GetDeliveryPlanQueryHandler.Map(row,
            [new DeliveryPlanLineDto { OrderedQuantity = 10, RemainingQuantity = 10 }], Date,
            Date.ToDateTime(TimeOnly.MinValue));

        Assert.Null(result.PromisedDeliveryDate);
        Assert.False(result.IsOverdue);
        Assert.Equal(DeliveryPlanProgress.NotDelivered, result.Progress);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SalesEmployee_UsesLatestActiveAssignmentThenCurrentSale(bool hasAssignment)
    {
        var current = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, FullName = "Current sale" };
        var latest = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, FullName = "Assigned sale" };
        var order = Order();
        order.Customer = new Customer { CompanyId = company, CurrentSaleId = current.EmployeeId };
        if (hasAssignment)
        {
            order.Customer.CustomerAssignments.Add(new CustomerAssignment { CompanyId = company, EmployeeId = current.EmployeeId, CreatedDate = new DateTime(2026, 10, 1) });
            order.Customer.CustomerAssignments.Add(new CustomerAssignment { CompanyId = company, EmployeeId = latest.EmployeeId, CreatedDate = new DateTime(2026, 10, 2) });
        }
        order.Customer.CustomerAssignments.Add(new CustomerAssignment { CompanyId = company, EmployeeId = current.EmployeeId, IsActive = false, CreatedDate = new DateTime(2026, 10, 3) });
        order.Customer.CustomerAssignments.Add(new CustomerAssignment { CompanyId = Guid.NewGuid(), EmployeeId = current.EmployeeId, CreatedDate = new DateTime(2026, 10, 4) });
        var row = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            Array.Empty<MfgOrderPO>().AsQueryable(), Array.Empty<DeliveryOrderDetail>().AsQueryable(), company, new[] { current, latest }.AsQueryable()));
        Assert.Equal(hasAssignment ? latest.FullName : current.FullName, row.SalesEmployeeFullName);
        row = new DeliveryPlanSource { SalesEmployeeFullName = row.SalesEmployeeFullName, PromisedDate = Date.ToDateTime(TimeOnly.MinValue) };
        Assert.Equal(row.SalesEmployeeFullName, GetDeliveryPlanQueryHandler.Map(row, [], Date, Date.ToDateTime(TimeOnly.MinValue)).SalesEmployeeFullName);
    }

    [Theory]
    [InlineData("missing-customer")]
    [InlineData("other-customer-company")]
    [InlineData("inactive-customer")]
    [InlineData("missing-sale")]
    [InlineData("other-employee-company")]
    [InlineData("inactive-employee")]
    public void SalesEmployee_ReturnsNullForMissingOrInvalidOwner(string scenario)
    {
        var employee = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, FullName = "Sale" };
        var order = Order();
        order.Customer = new Customer { CompanyId = company, CurrentSaleId = employee.EmployeeId };
        if (scenario == "missing-customer") order.Customer = null!;
        if (scenario == "other-customer-company") order.Customer.CompanyId = Guid.NewGuid();
        if (scenario == "inactive-customer") order.Customer.IsActive = false;
        if (scenario == "missing-sale") order.Customer.CurrentSaleId = null;
        if (scenario == "other-employee-company") employee.CompanyId = Guid.NewGuid();
        if (scenario == "inactive-employee") employee.IsActive = false;
        var row = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            Array.Empty<MfgOrderPO>().AsQueryable(), Array.Empty<DeliveryOrderDetail>().AsQueryable(), company, new[] { employee }.AsQueryable()));
        Assert.Null(row.SalesEmployeeFullName);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Handler_DeniesMissingAuthenticationOrCompanyBeforeQuerying(bool authenticated, bool hasCompany)
    {
        var user = new TestUser { IsAuthenticated = authenticated, CompanyId = hasCompany ? company : null };
        var handler = new GetDeliveryPlanQueryHandler(null!, user, null!, null!, null!, null!);
        var result = await handler.Handle(new GetDeliveryPlanQuery(), CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact]
    public void Tomorrow_UsesVietnamTimeAcrossUtcMidnight()
        => Assert.Equal(new DateOnly(2026, 10, 7), GetDeliveryPlanQueryHandler.Tomorrow(new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void DateFilter_KeepsCompletedOnDateAndOnlyUnfinishedOverdue()
    {
        var target = Source(Date, 0);
        var overdue = Source(Date.AddDays(-1), 3);
        var rows = new[] { target, overdue, Source(Date.AddDays(-1), 0), Source(Date.AddDays(1), 3), new DeliveryPlanSource() }.AsQueryable();
        Assert.Equal(new[] { target, overdue }, GetDeliveryPlanQueryHandler.ForDate(rows, Date, true).ToArray());
        Assert.Equal(new[] { target }, GetDeliveryPlanQueryHandler.ForDate(rows, Date, false).ToArray());
    }

    [Fact]
    public void Projection_GroupsPoByLatestActivePromiseAndCountsEffectiveDeliveries()
    {
        var order = Order();
        var line = Line(order, 10);
        var links = new[] { Link(line, Date.AddDays(-1)), Link(line, Date), Link(line, Date.AddDays(2), "Canceled"), Link(line, Date.AddDays(3), active: false) };
        var rows = new[] { Delivery(line, 4, "Completed"), Delivery(line, 6, "Pending", true), Delivery(line, 10, "Canceled") };
        var result = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(), links.AsQueryable(), rows.AsQueryable(), company, NoEmployees));
        Assert.Equal(Date.ToDateTime(TimeOnly.MinValue), result.PromisedDate);
        Assert.Equal(10, result.DeliveredQuantity);
        Assert.Equal(0, result.RemainingQuantity);
    }

    [Fact]
    public void Projection_OverdeliveryOfOneProductDoesNotCoverAnotherProduct()
    {
        var order = Order();
        var a = Line(order, 10);
        Line(order, 10);
        var result = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(), new[] { Link(a, Date.AddDays(-1)) }.AsQueryable(),
            new[] { Delivery(a, 20, "Completed") }.AsQueryable(), company, NoEmployees));
        Assert.Equal(10, result.RemainingQuantity);
        Assert.Single(GetDeliveryPlanQueryHandler.ForDate(new[] { result }.AsQueryable(), Date, true));
    }

    [Theory]
    [InlineData("Delivering")]
    [InlineData("Delivered")]
    [InlineData("Completed")]
    [InlineData(" delivered ")]
    public void ConfirmedPo_CountsLegacyPendingButExcludesCanceledInactiveAndOtherCompany(string status)
    {
        var order = Order(); order.Status = status; order.ExternalId = "DHG260700270";
        var line = Line(order, 150);
        var valid = Delivery(line, 150, "Pending", true); valid.DeliveryOrder.ExternalId = "PGH260700427";
        var inactive = Delivery(line, 150, "Pending"); inactive.IsActive = false;
        var other = Delivery(line, 150, "Completed"); other.DeliveryOrder.CompanyId = Guid.NewGuid();
        var rows = new[] { valid, Delivery(line, 150, "Cancelled"), inactive, other }.AsQueryable();
        Assert.Equal(150m, DeliveryPlanSourceQuery.CountedAsDelivered(rows, company).Sum(x => x.Quantity));
        var projected = DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            new[] { Link(line, Date.AddDays(-1)) }.AsQueryable(), rows, company, NoEmployees);
        if (!string.Equals(status.Trim(), "Delivering", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Empty(projected);
            return;
        }
        var result = Assert.Single(projected);
        Assert.Equal(150m, result.DeliveredQuantity);
        Assert.Equal(0m, result.RemainingQuantity);
        Assert.Empty(GetDeliveryPlanQueryHandler.ForDate(new[] { result }.AsQueryable(), Date, true));
        Assert.Single(GetDeliveryPlanQueryHandler.ForDate(new[] { result }.AsQueryable(), Date.AddDays(-1), true));
        Assert.Equal(DeliveryPlanProgress.Delivered, DeliveryPlanReadModel.Progress(result.DeliveredQuantity, result.RemainingQuantity));
    }

    [Fact]
    public void ConfirmedPo_DoesNotInventQuantityOrCoverMissingProduct()
    {
        var order = Order();
        var line = Line(order, 10); Line(order, 10);
        var result = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            new[] { Link(line, Date) }.AsQueryable(), new[] { Delivery(line, 20, "Pending") }.AsQueryable(), company, NoEmployees));
        Assert.Equal(10m, result.RemainingQuantity);
        Assert.Equal(DeliveryPlanProgress.PartiallyDelivered, DeliveryPlanReadModel.Progress(result.DeliveredQuantity, result.RemainingQuantity));
    }

    [Fact]
    public void Projection_ExcludesOtherCompanyAndCanceledPoAndMissingPromise()
    {
        var valid = Order();
        var line = Line(valid, 10);
        var other = Order(); other.CompanyId = Guid.NewGuid();
        var canceled = Order(); canceled.Status = "Cancelled";
        var noPromise = Order(); Line(noPromise, 10);
        var result = DeliveryPlanSourceQuery.Project(new[] { valid, other, canceled, noPromise }.AsQueryable(),
            new[] { Link(line, Date) }.AsQueryable(), Array.Empty<DeliveryOrderDetail>().AsQueryable(), company, NoEmployees);
        Assert.Equal(valid.MerchandiseOrderId, Assert.Single(GetDeliveryPlanQueryHandler.ForDate(result, Date, true)).Id);
        Assert.Equal(new[] { valid.MerchandiseOrderId, noPromise.MerchandiseOrderId },
            GetDeliveryPlanQueryHandler.ForDate(result, Date, true, true).Select(x => x.Id).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Query_TranslatesCorrelatedTotalsAndLatestPromiseToPostgres(bool includeAllOrders)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = DeliveryPlanSourceQuery.Project(db.MerchandiseOrders, db.MfgOrderPOs, db.DeliveryOrderDetails, company, db.Employees);
        var sql = GetDeliveryPlanQueryHandler.ForDate(query, Date, true, includeAllOrders).OrderBy(x => x.PromisedDate).Take(20).ToQueryString();
        Assert.Contains("FullName", sql);
        Assert.Contains("\"Customer\".\"CustomerAssignment\"", sql);
        Assert.Contains("max(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cancelled", sql);
        Assert.Contains("CompanyId", sql);
        Assert.DoesNotContain("UnitPriceAgreed", sql);
        Assert.DoesNotContain("BaseCostSnapshot", sql);
    }

    [Theory]
    [InlineData(0, 10, DeliveryPlanProgress.NotDelivered)]
    [InlineData(4, 6, DeliveryPlanProgress.PartiallyDelivered)]
    [InlineData(10, 0, DeliveryPlanProgress.Delivered)]
    public void Progress_UsesCompletedQuantity(decimal delivered, decimal remaining, DeliveryPlanProgress expected)
        => Assert.Equal(expected, DeliveryPlanReadModel.Progress(delivered, remaining));

    [Fact]
    public void DeliveringPo_MatchesPartialTimelineAndExcludesCanceledDelivery()
    {
        var order = Order(); order.ExternalId = "DHG260900239";
        var line = Line(order, 125);
        var rows = new[] { Delivery(line, 123.5m, "Pending"), Delivery(line, 1.5m, " CANCELED ") }.AsQueryable();
        var result = Assert.Single(DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            new[] { Link(line, Date) }.AsQueryable(), rows, company, NoEmployees));
        Assert.Equal(123.5m, result.DeliveredQuantity);
        Assert.Equal(1.5m, result.RemainingQuantity);
        Assert.Equal(DeliveryPlanProgress.PartiallyDelivered, DeliveryPlanReadModel.Progress(result.DeliveredQuantity, result.RemainingQuantity));
    }

    [Theory]
    [InlineData("Delivered")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Canceled")]
    [InlineData(" DELIVERED ")]
    public void ClosedPo_IsExcludedEvenWhenNoDeliveryAndPromisedOnSelectedDate(string status)
    {
        var order = Order(); order.Status = status;
        var line = Line(order, 125);
        var rows = DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            new[] { Link(line, Date) }.AsQueryable(), Array.Empty<DeliveryOrderDetail>().AsQueryable(), company, NoEmployees);
        Assert.Empty(rows);
        Assert.Empty(GetDeliveryPlanQueryHandler.ForDate(rows, Date, true));
        Assert.Empty(GetDeliveryPlanQueryHandler.ForDate(rows, Date.AddDays(1), true));
    }

    [Theory]
    [InlineData(false, "Delivering")]
    [InlineData(true, "Paused")]
    public void DisplayStatus_MatchesSaleOrderWhileProgressRemainsQuantityBased(bool paused, string expected)
    {
        var row = new DeliveryPlanSource { Status = "Delivering", IsDeliveryPaused = paused,
            PromisedDate = Date.ToDateTime(TimeOnly.MinValue), DeliveryPausedFrom = Date.ToDateTime(TimeOnly.MinValue),
            DeliveryPausedTo = Date.ToDateTime(TimeOnly.MinValue) };
        var result = GetDeliveryPlanQueryHandler.Map(row,
            new[] { new DeliveryPlanLineDto { OrderedQuantity = 125, DeliveredQuantity = 100, RemainingQuantity = 25 } },
            Date.AddDays(1), Date.ToDateTime(TimeOnly.MinValue));
        Assert.Equal(expected, result.Status);
        Assert.Equal(DeliveryPlanProgress.PartiallyDelivered, result.Progress);
    }

    [Theory]
    [InlineData("KH_VIETAUS", false)]
    [InlineData(" kh_vietaus ", false)]
    [InlineData("KH_VIETAUS", true)]
    public void InternalCustomer_IsExcludedBySnapshotOrCurrentCustomerCode(string code, bool currentCustomer)
    {
        var order = Order();
        if (currentCustomer)
            order.Customer = new HRM.Domain.Entities.CustomerSchema.Customer { ExternalId = code };
        else
            order.CustomerExternalIdSnapshot = code;
        var line = Line(order, 10);
        var result = DeliveryPlanSourceQuery.Project(new[] { order }.AsQueryable(),
            new[] { Link(line, Date) }.AsQueryable(), Array.Empty<DeliveryOrderDetail>().AsQueryable(), company, NoEmployees);
        Assert.Empty(result);
    }

    [Fact]
    public void ImportedQuantity_UsesCurrentFinishedAndDefectiveStockInActiveCompanyShelves()
    {
        WarehouseShelfStock Stock(Guid owner, decimal kg, StockType type, bool active = true)
            => new() { CompanyId = owner, Code = "SP-A", QtyKg = kg, StockType = type,
                WarehouseShelves = new WarehouseShelves { IsActive = active } };
        var rows = new[]
        {
            Stock(company, 100m, StockType.FinishedGood),
            Stock(company, 25m, StockType.DefectiveFinishedGood),
            Stock(company, 999m, StockType.FinishedGood, false),
            Stock(Guid.NewGuid(), 999m, StockType.FinishedGood),
            Stock(company, 999m, StockType.RawMaterial),
            new WarehouseShelfStock { CompanyId = company, Code = "SP-A", QtyKg = 999m,
                StockType = StockType.FinishedGood, WarehouseShelves = null }
        };
        Assert.Equal(125m, DeliveryPlanReadModel.CurrentProductStock(rows.AsQueryable(), company).Sum(x => x.QtyKg));
        Assert.Empty(DeliveryPlanReadModel.CurrentProductStock(rows.AsQueryable(), Guid.NewGuid()));
        var breakdown = Assert.Single(DeliveryPlanReadModel.CurrentProductStockTotals(rows.AsQueryable(), company));
        Assert.Equal(100m, breakdown.FinishedGoodQuantity);
        Assert.Equal(25m, breakdown.DefectiveFinishedGoodQuantity);
        Assert.Empty(DeliveryPlanReadModel.CurrentProductStockTotals(rows.AsQueryable(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(StockType.FinishedGood, 12, 0)]
    [InlineData(StockType.DefectiveFinishedGood, 0, 12)]
    public void StockBreakdown_MissingStockTypeIsZero(StockType stockType, decimal finished, decimal defective)
    {
        var stocks = new[] { new WarehouseShelfStock
        {
            CompanyId = company, Code = "SP-A", QtyKg = 12m, StockType = stockType,
            WarehouseShelves = new WarehouseShelves { IsActive = true }
        }}.AsQueryable();
        var stock = Assert.Single(DeliveryPlanReadModel.CurrentProductStockTotals(stocks, company));
        Assert.Equal(finished, stock.FinishedGoodQuantity);
        Assert.Equal(defective, stock.DefectiveFinishedGoodQuantity);
        var empty = new DeliveryPlanLineDto();
        Assert.Equal(0m, empty.StockBreakdown.FinishedGoodQuantity);
        Assert.Equal(0m, empty.StockBreakdown.DefectiveFinishedGoodQuantity);
    }

    [Fact]
    public void StockBreakdown_TranslatesToOneGroupedPostgresQuery()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = DeliveryPlanReadModel.CurrentProductStockTotals(db.WarehouseShelfStocks.AsNoTracking(), company)
            .Where(x => new[] { "SP-A" }.Contains(x.Code)).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("CASE", sql);
        Assert.Contains("\"companyId\"", sql);
        Assert.Contains("\"isActive\"", sql);
        Assert.Contains("FinishedGoodQuantity", sql);
        Assert.Contains("DefectiveFinishedGoodQuantity", sql);
    }

    [Theory]
    [InlineData(-1, 25, true)]
    [InlineData(0, 25, false)]
    [InlineData(1, 25, false)]
    [InlineData(-1, 0, false)]
    public void Overdue_UsesPromiseAgainstTodayNotSelectedPlanDate(int offset, decimal remaining, bool overdue)
    {
        var now = new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc); // 07/10 tại UTC+7
        var today = new DateOnly(2026, 10, 7);
        var row = new DeliveryPlanSource { Status = "Delivering", PromisedDate = today.AddDays(offset).ToDateTime(TimeOnly.MinValue) };
        var lines = new[] { new DeliveryPlanLineDto { RemainingQuantity = remaining } };
        Assert.Equal(overdue, GetDeliveryPlanQueryHandler.Map(row, lines, today.AddDays(5), now).IsOverdue);
        Assert.Equal(overdue, GetDeliveryPlanQueryHandler.Map(row, lines, today.AddDays(-5), now).IsOverdue);
    }

    private static DeliveryPlanSource Source(DateOnly date, decimal remaining)
        => new() { PromisedDate = date.ToDateTime(TimeOnly.MinValue), RemainingQuantity = remaining };

    private MerchandiseOrder Order() => new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true, Status = "Delivering" };

    private static MerchandiseOrderDetail Line(MerchandiseOrder order, decimal quantity)
    {
        var line = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = order.MerchandiseOrderId, MerchandiseOrder = order, ExpectedQuantity = quantity };
        order.MerchandiseOrderDetails.Add(line);
        return line;
    }

    private MfgOrderPO Link(MerchandiseOrderDetail line, DateOnly date, string status = "Finished", bool active = true)
        => new() { IsActive = true, MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, Detail = line,
            ProductionOrder = new MfgProductionOrder { CompanyId = company, IsActive = active, Status = status, ExpectedDate = date.ToDateTime(TimeOnly.MinValue) } };

    private DeliveryOrderDetail Delivery(MerchandiseOrderDetail line, decimal quantity, string status, bool printed = false)
        => new() { MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, MerchandiseOrderDetail = line, Quantity = quantity,
            DeliveryOrder = new DeliveryOrder { CompanyId = company, Status = status, HasPrinted = printed } };

    private sealed class TestUser : ICurrentUser
    {
        public bool IsAuthenticated { get; init; }
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; }
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => ["Admin"];
        public bool IsInRole(string role) => role == "Admin";
    }
}
