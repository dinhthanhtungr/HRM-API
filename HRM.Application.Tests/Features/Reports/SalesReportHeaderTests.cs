using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Reporting.Costing;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Identity;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Merchadises;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Reports;

public sealed class SalesReportHeaderTests
{
    [Theory]
    [InlineData(2026, 9, 20)]
    [InlineData(2026, 12, 31)]
    public async Task Overdue_UsesCurrentDate_AndHeaderMatchesDrawer(int year, int month, int day)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = ["Developer"] };
        var today = new Clock().Now.Date;
        var orders = Enumerable.Range(0, 3).Select(_ => new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Customer = new Customer { CustomerId = Guid.NewGuid(), ExternalId = "KH_EXTERNAL", IsActive = true },
            CustomerExternalIdSnapshot = "KH_EXTERNAL",
            CreateDate = new(2026, 3, 1), Status = "Processing", OrderType = OrderType.Merchandise
        }).ToArray();
        foreach (var order in orders) order.CustomerId = order.Customer!.CustomerId;
        var details = orders.Select((order, index) => new MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = order.MerchandiseOrderId,
            MerchandiseOrder = order, ExpectedQuantity = 10,
            DeliveryRequestDate = today.AddDays(index - 1).AddHours(12), UnitPriceAgreed = 100
        }).ToArray();
        store.Seed(nameof(db.MerchandiseOrders), orders);
        store.Seed(nameof(db.MerchandiseOrderDetails), details);
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(year, month, day);
        var header = await new GetSalesReportHeaderQueryHandler(db, user,
            new CurrentUserPermissionService(user), new Clock()).Handle(new() { From = from, To = to }, default);
        Assert.True(header.Success);
        Assert.Equal(3, header.Data!.PendingOrderCount);
        Assert.Equal(1, header.Data.OverdueOrderCount);
        var handler = new GetSalesReportOrdersQueryHandler(db, user, new Clock());
        var pending = await handler.Handle(new() { Metric = "pendingOrder", From = from, To = to }, default);
        Assert.True(pending.Success);
        Assert.Equal(header.Data.PendingOrderCount, pending.Data!.TotalCount);
        var overdue = await handler.Handle(new() { Metric = "overdueOrder", From = from, To = to }, default);
        Assert.True(overdue.Success);
        Assert.Equal(header.Data.OverdueOrderCount, overdue.Data!.TotalCount);
        Assert.Equal(orders[0].MerchandiseOrderId, Assert.Single(overdue.Data.Items).OrderId);
    }

    [Fact]
    public void ExpectedPendingMonths_UseScheduledDateAndReconcileToTotal()
    {
        var period = new SalesReportPeriod(new(2026, 9, 15), new(2026, 12, 1));
        var details = Enumerable.Range(0, 7).Select(i => new SalesHeaderOrderLine
        {
            OrderId = Guid.NewGuid(), DetailId = Guid.NewGuid(), OrderDate = new(2026, 9, 1),
            RequestedDate = new(2026, 9, 20), Quantity = 10, UnitPrice = 2,
            Currency = "USD", ExchangeRate = 25000,
            ExpectedDeliveryDate = i switch
            {
                0 => new DateTime(2026, 10, 1),
                1 => null,
                2 => new DateTime(2026, 12, 1),
                3 => new DateTime(2026, 9, 14),
                _ => new DateTime(2026, 11, 1)
            },
            OrderStatus = i == 4 ? "Paused" : i == 5 ? "Completed" : "Pending"
        }).ToArray();
        var delivered = new Dictionary<Guid, decimal> { [details[0].DetailId] = 3, [details[6].DetailId] = 12 };
        var result = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period, [], 0, details,
            delivered, 0, null, new(2026, 10, 20));
        Assert.Collection(result.ExpectedPendingByMonth,
            m => { Assert.Equal(new DateTime(2026, 9, 1), m.Month); Assert.Equal(500000m, m.Amount); },
            m => { Assert.Equal(new DateTime(2026, 10, 1), m.Month); Assert.Equal(350000m, m.Amount); });
        Assert.Equal(result.ExpectedPendingAmountInPeriod, result.ExpectedPendingByMonth.Sum(m => m.Amount));
        var empty = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period, [], 0, [],
            new Dictionary<Guid, decimal>(), 0, null, new(2026, 10, 20));
        Assert.Empty(empty.ExpectedPendingByMonth);
        Assert.Equal(0m, empty.ExpectedPendingAmountInPeriod);
    }

    [Fact]
    public void Pending_ExcludesPreMarchAndClosedOrders_FromAllPendingMetrics()
    {
        var period = new SalesReportPeriod(new(2026, 10, 1), new(2026, 11, 1));
        var details = new[] { (new DateTime(2026, 2, 28), "New"),
            (new DateTime(2026, 3, 1), "Delivered"), (new DateTime(2026, 3, 1), "Completed"),
            (new DateTime(2026, 3, 1), "Cancelled"), (new DateTime(2026, 3, 1), "Processing") }
            .Select(x => new SalesHeaderOrderLine { OrderId = Guid.NewGuid(), DetailId = Guid.NewGuid(),
                OrderDate = x.Item1, OrderStatus = x.Item2, RequestedDate = new(2026, 10, 2),
                Quantity = 10, UnitPrice = 100 }).ToArray();
        var result = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period, [], 0, details,
            new Dictionary<Guid, decimal>(), 0, null, new DateTime(2026, 10, 9));
        Assert.Equal(1, result.PendingOrderCount);
        Assert.Equal(1, result.OverdueOrderCount);
        Assert.Equal(10m, result.PendingQuantity);
        Assert.Equal(1000m, result.PendingAmount);
        Assert.Equal(1000m, result.ExpectedPendingAmountInPeriod);
    }

    [Fact]
    public void AverageSellingPrice_UsesDeliveredQuantityWeightedPrices_AndIsNullWithoutQuantity()
    {
        var period = new SalesReportPeriod(new(2026, 10, 1), new(2026, 11, 1));
        var header = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period,
            [new DeliveryRevenueLine { Quantity = 2, RevenueAmountVnd = 200 },
             new DeliveryRevenueLine { Quantity = 8, RevenueAmountVnd = 1600 }], 0, [],
            new Dictionary<Guid, decimal>(), 0, null, new DateTime(2026, 10, 9));
        Assert.Equal(180m, header.AverageSellingPrice);
        var empty = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period, [], 0, [],
            new Dictionary<Guid, decimal>(), 0, null, new DateTime(2026, 10, 9));
        Assert.Null(empty.AverageSellingPrice);
    }

    [Theory]
    [InlineData("ACCUser")]
    [InlineData("Admin")]
    [InlineData("SaleUser")]
    public async Task BodyHandler_RejectsRolesOutsideDeveloperPresident(string role)
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = [role] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetSalesReportBodyQueryHandler(
            db, user, new CurrentUserPermissionService(user), new Clock()).Handle(new(), default));
    }

    [Fact]
    public async Task BodyHandler_RejectsOtherCompanyAndInvalidDimension()
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = ["Developer"] };
        var handler = new GetSalesReportBodyQueryHandler(db, user, new CurrentUserPermissionService(user), new Clock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new()
            { CompanyId = Guid.NewGuid() }, default));
        Assert.False((await handler.Handle(new() { GroupBy = (SalesReportDimension)999 }, default)).Success);
        Assert.False((await handler.Handle(new() { From = new(2026, 10, 1), To = new(2026, 9, 1) }, default)).Success);
    }

    [Fact]
    public void SixMonthTrend_FillsMissingMonths_AndStopsAtSelectedDay()
    {
        var trend = SalesReportRevenueTrend.Build(new(2026, 10, 9),
            [new DeliveryRevenueLine { RevenueDate = new(2026, 5, 1), RevenueAmountVnd = 10 },
             new DeliveryRevenueLine { RevenueDate = new(2026, 4, 30), RevenueAmountVnd = 100 },
             new DeliveryRevenueLine { RevenueDate = new(2026, 10, 9, 23, 0, 0), RevenueAmountVnd = 20 },
             new DeliveryRevenueLine { RevenueDate = new(2026, 10, 10), RevenueAmountVnd = 200 }]);
        Assert.Equal(6, trend.Count);
        Assert.Equal(new DateTime(2026, 5, 1), trend[0].Month);
        Assert.Equal(10m, trend[0].Revenue);
        Assert.Equal(0m, trend[1].Revenue);
        Assert.Equal(20m, trend[5].Revenue);
        Assert.Equal(new DateTime(2026, 10, 9), trend[5].To);
        Assert.Equal(6, SalesReportRevenueTrend.Build(new(9999, 12, 30), []).Count);
    }

    [Fact]
    public void BodyTotals_ReconcileGroups_AndMaskCostsWhenPermissionIsAbsent()
    {
        var period = new SalesReportPeriod(new(2026, 10, 1), new(2026, 11, 1));
        SalesReportBodyLine[] lines = [new("g1", "G1", "s1", "S1", 100, 2, 40),
            new("g1", "G1", "s2", "S2", 200, 3, 100), new("g2", "G2", "s3", "S3", 50, 1, 20)];
        var body = SalesReportBodyBuilder.Build(Guid.NewGuid(), period, SalesReportDimension.Sale, true, lines);
        Assert.Equal(350m, body.Totals.Revenue);
        Assert.Equal(160m, body.Totals.CostOfSales);
        Assert.Equal(190m, body.Totals.Profit);
        Assert.Equal(body.Totals.Revenue, body.Groups.Sum(g => g.Metrics.Revenue));
        Assert.All(body.Groups, g => Assert.Equal(g.Metrics.Revenue, g.Rows.Sum(r => r.Metrics.Revenue)));
        var hidden = SalesReportBodyBuilder.Build(Guid.NewGuid(), period, SalesReportDimension.Sale, false, lines);
        Assert.Null(hidden.Totals.CostOfSales);
        Assert.Null(hidden.Totals.Profit);
        Assert.All(hidden.Groups.SelectMany(g => g.Rows), r => Assert.Null(r.Metrics.MarginPercent));
    }

    [Fact]
    public void ExpectedRevenue_UsesScheduledPeriodAndStatus_WithoutChangingTotalPending()
    {
        var period = new SalesReportPeriod(new(2026, 9, 1), new(2026, 10, 1));
        SalesHeaderOrderLine Line(DateTime requested, DateTime? expected = null, string? status = null)
            => new() { DetailId = Guid.NewGuid(), OrderId = Guid.NewGuid(),
                OrderDate = period.From, RequestedDate = requested, ExpectedDeliveryDate = expected, OrderStatus = status,
                Quantity = 10, UnitPrice = 2, Currency = "USD", ExchangeRate = 25000 };
        var details = new[] {
            Line(period.From),
            Line(period.From.AddMonths(-1), period.To),
            Line(period.From, period.ToExclusive),
            Line(period.From.AddDays(-1)),
            Line(period.From, status: "Completed"),
            Line(period.From, status: "Delivered"),
            Line(period.From, status: "Paused")
        };
        var header = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period,
            [new DeliveryRevenueLine { RevenueAmountVnd = 100m }], 0, details,
            new Dictionary<Guid, decimal> { [details[0].DetailId] = 3 }, 0, null, new DateTime(2026, 10, 9));
        Assert.Equal(850000m, header.ExpectedPendingAmountInPeriod);
        Assert.Equal(850100m, header.ExpectedRevenueInPeriod);
        Assert.Equal(2350000m, header.PendingAmount);
    }

    [Theory]
    [InlineData("QCUser")]
    [InlineData("Admin")]
    [InlineData("LabUser")]
    public async Task SalesDashboard_RejectsOtherRoles(string role)
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = [role] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetExecutivePnLDashboardBySalesQueryHandler(db, user, new CurrentUserPermissionService(user))
                .Handle(new GetExecutivePnLDashboardBySalesQuery(), default));
    }

    [Theory]
    [InlineData("President")]
    [InlineData("Developer")]
    [InlineData("ACCUser")]
    public async Task HeaderAndPnlSalesReader_UseSameAttributionAndCost_AndSaleCannotReadAnotherSale(string readerRole)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = [readerRole] };
        var company = user.CompanyId!.Value;
        var saleId = Guid.NewGuid();
        var group = new Group { GroupId = Guid.NewGuid(), CompanyId = company, Name = "Sales" };
        var customer = new Customer { CustomerId = Guid.NewGuid(), IsActive = true, ExternalId = "CUSTOMER",
            CompanyId = company, CustomerGroup = "Distributor" };
        var order = new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
            CustomerId = customer.CustomerId, Customer = customer, CreateDate = new(2026, 4, 1), Currency = "VND" };
        var detail = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(),
            MerchandiseOrderId = order.MerchandiseOrderId, MerchandiseOrder = order,
            ExpectedQuantity = 10, UnitPriceAgreed = 100, TotalPriceAgreed = 1000,
            DeliveryRequestDate = new(2026, 9, 15), BaseCostSnapshot = 40 };
        var delivery = new DeliveryOrderDetail { MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
            MerchandiseOrderDetail = detail, Quantity = 3,
            DeliveryOrder = new DeliveryOrder { CompanyId = company, IsActive = true,
                CreatedDate = new(2026, 9, 20), CustomerExternalIdSnapShot = "CUSTOMER" } };
        var testOrders = new List<MerchandiseOrder> { order };
        foreach (var type in new[] { OrderType.Complaint, OrderType.SampleRequest })
        {
            testOrders.AddRange([
                new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
                    CustomerId = customer.CustomerId, CreateDate = new(2026, 9, 1), OrderType = type,
                    Status = type == OrderType.Complaint ? "Completed" : "New" },
                new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
                    CustomerId = customer.CustomerId, CreateDate = new(2026, 9, 1), OrderType = type,
                    Status = "Cancelled" },
                new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
                    CustomerId = customer.CustomerId, CreateDate = new(2026, 9, 1), OrderType = type }]);
        }
        var excludedPendingOrders = new[] { (new DateTime(2026, 4, 1), "Completed"),
            (new DateTime(2026, 4, 1), "Delivered"), (new DateTime(2026, 2, 28), "New") }
            .Select(x => new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
                CustomerId = customer.CustomerId, CreateDate = x.Item1, Status = x.Item2 }).ToArray();
        testOrders.AddRange(excludedPendingOrders);
        store.Seed(nameof(db.MerchandiseOrders), testOrders.ToArray());
        store.Seed(nameof(db.MerchandiseOrderDetails), excludedPendingOrders.Select(x =>
            new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(),
                MerchandiseOrderId = x.MerchandiseOrderId, MerchandiseOrder = x, ExpectedQuantity = 100,
                DeliveryRequestDate = new(2026, 9, 1) }).Append(detail).ToArray());
        store.Seed(nameof(db.DeliveryOrderDetails), delivery,
            new DeliveryOrderDetail { MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                MerchandiseOrderDetail = detail, Quantity = 1,
                DeliveryOrder = new DeliveryOrder { CompanyId = company, IsActive = true,
                    CreatedDate = new(2026, 5, 15), CustomerExternalIdSnapShot = "CUSTOMER" } });
        store.Seed(nameof(db.UserRoles), new ApplicationUserRole
        {
            User = new ApplicationUser { EmployeeId = saleId,
                Employee = new Employee { EmployeeId = saleId, CompanyId = company, IsActive = true, FullName = "Sale" } },
            Role = new ApplicationRole { Name = "SaleUser" }
        });
        store.Seed(nameof(db.MemberInGroups), new MemberInGroup { Profile = saleId, Group = group, GroupId = group.GroupId });
        var assignment = new CustomerAssignment { CustomerId = customer.CustomerId,
            Customer = customer, EmployeeId = saleId, CompanyId = company };
        customer.CustomerAssignments.Add(assignment);
        store.Seed(nameof(db.CustomerAssignments), assignment);
        var request = new GetSalesReportHeaderQuery { From = new(2026, 9, 1), To = new(2026, 9, 30) };
        var result = await new GetSalesReportHeaderQueryHandler(db, user,
            new CurrentUserPermissionService(user), new Clock()).Handle(request, default);
        var pnl = Assert.Single(await new ExecutivePnLSalesReader(db, user).ReadMonthlyAsync(
            new ExecutivePnLSaleFilter { CompanyId = company, FromMonth = request.From!.Value, ToMonth = request.From.Value }));
        Assert.Equal(pnl.Revenue, result.Data!.Revenue);
        Assert.Equal(pnl.CostOfSales, result.Data.CostOfSales);
        Assert.Equal(300m, result.Data.Revenue);
        Assert.Equal(120m, result.Data.CostOfSales);
        Assert.Equal(600m, result.Data.PendingAmount);
        Assert.Equal(0m, result.Data.PreviousRevenue);
        Assert.Equal(0, result.Data.NewOrderCount);
        Assert.Equal(0, result.Data.MerchandisePoCount);
        Assert.Equal(1, result.Data.ComplaintPoCount);
        Assert.Equal(1, result.Data.SamplePoCount);
        Assert.Equal(1, result.Data.CompletedPoCount);
        Assert.Equal(1, result.Data.IncompletePoCount);
        Assert.Equal(6, result.Data.MonthlyRevenue.Count);
        Assert.Equal(300m, result.Data.MonthlyRevenue.Last().Revenue);
        Assert.Equal(100m, result.Data.MonthlyRevenue.Single(x => x.Month.Month == 5).Revenue);
        if (readerRole != "ACCUser")
        {
            foreach (var dimension in Enum.GetValues<SalesReportDimension>())
            {
                var body = await new GetSalesReportBodyQueryHandler(db, user,
                    new CurrentUserPermissionService(user), new Clock()).Handle(new()
                    { From = request.From, To = request.To, GroupBy = dimension }, default);
                Assert.Equal(result.Data.Revenue, body.Data!.Totals.Revenue);
                Assert.Equal(result.Data.CostOfSales, body.Data.Totals.CostOfSales);
                Assert.Equal(result.Data.Profit, body.Data.Totals.Profit);
                if (dimension == SalesReportDimension.CustomerType)
                    Assert.Equal("Distributor", Assert.Single(Assert.Single(body.Data.Groups).Rows).Label);
            }
            var noProfitUser = new User { Roles = [readerRole], CompanyId = company, HasExplicitPermissionSet = true };
            var noProfit = await new GetSalesReportBodyQueryHandler(db, noProfitUser,
                new CurrentUserPermissionService(noProfitUser), new Clock()).Handle(new()
                { From = request.From, To = request.To }, default);
            Assert.Equal(300m, noProfit.Data!.Totals.Revenue);
            Assert.False(noProfit.Data.CanViewProfit);
            Assert.Null(noProfit.Data.Totals.CostOfSales);
            Assert.Null(Assert.Single(Assert.Single(noProfit.Data.Groups).Rows).Metrics.Profit);
        }
        Assert.Equal(1, result.Data.OverdueOrderCount);

        var orderDrawer = new GetSalesReportOrdersQueryHandler(db, user, new Clock());
        foreach (var (metric, expectedCount, type) in new[]
        {
            ("merchandisePo", result.Data.MerchandisePoCount, OrderType.Merchandise),
            ("complaintPo", result.Data.ComplaintPoCount, OrderType.Complaint),
            ("samplePo", result.Data.SamplePoCount, OrderType.SampleRequest)
        })
        {
            var drawer = await orderDrawer.Handle(new() { Metric = metric, From = request.From,
                To = request.To, PageSize = 1 }, default);
            Assert.True(drawer.Success);
            Assert.Equal(expectedCount, drawer.Data!.TotalCount);
            Assert.Equal(Math.Min(expectedCount, 1), drawer.Data.Items.Count);
            Assert.All(drawer.Data.Items, item =>
            {
                Assert.Equal(type, item.OrderType);
                Assert.InRange(item.CreatedDate, request.From!.Value, request.To!.Value.AddDays(1).AddTicks(-1));
                Assert.Null(item.PendingQuantity);
                Assert.Null(item.DeliveredQuantityInPeriod);
            });
        }
        foreach (var (metric, count) in new[] { ("completedPo", result.Data.CompletedPoCount),
            ("incompletePo", result.Data.IncompletePoCount), ("pendingOrder", result.Data.PendingOrderCount),
            ("overdueOrder", result.Data.OverdueOrderCount), ("deliveredOrder", result.Data.DeliveredOrderCount) })
        {
            var drawer = await orderDrawer.Handle(new() { Metric = metric, From = request.From,
                To = request.To, PageSize = 1 }, default);
            Assert.True(drawer.Success);
            Assert.Equal(count, drawer.Data!.TotalCount);
            Assert.Single(drawer.Data.Items);
            if (metric is "pendingOrder" or "overdueOrder")
                Assert.Equal(6m, drawer.Data.Items[0].PendingQuantity);
            if (metric == "deliveredOrder")
                Assert.Equal(3m, drawer.Data.Items[0].DeliveredQuantityInPeriod);
            var nextPage = await orderDrawer.Handle(new() { Metric = metric, From = request.From,
                To = request.To, PageSize = 1, PageNumber = 2 }, default);
            Assert.Equal(count, nextPage.Data!.TotalCount);
            Assert.Empty(nextPage.Data.Items);
        }
        var customerDrawer = await new GetSalesReportCustomersQueryHandler(db, user, new Clock()).Handle(
            new() { Metric = "deliveredCustomer", From = request.From, To = request.To }, default);
        Assert.Equal(result.Data.CustomerCount, customerDrawer.Data!.TotalCount);
        Assert.Equal(3m, Assert.Single(customerDrawer.Data.Items).DeliveredQuantityInPeriod);

        var saleUser = new User { CompanyId = company, EmployeeId = saleId, Roles = ["SaleUser"] };
        var saleHeader = await new GetSalesReportHeaderQueryHandler(db, saleUser,
            new CurrentUserPermissionService(saleUser), new Clock()).Handle(request, default);
        Assert.Equal(300m, saleHeader.Data!.Revenue);
        Assert.Null(saleHeader.Data.CostOfSales);
        Assert.Null(saleHeader.Data.Profit);
        var otherSale = await new GetSalesReportHeaderQueryHandler(db, saleUser,
            new CurrentUserPermissionService(saleUser), new Clock()).Handle(
                new() { From = request.From, To = request.To, SalePerson = Guid.NewGuid() }, default);
        Assert.Equal(0m, otherSale.Data!.Revenue);
        Assert.Equal(0, otherSale.Data.ComplaintPoCount);
        Assert.Equal(0, otherSale.Data.SamplePoCount);
        foreach (var metric in new[] { "completedPo", "incompletePo", "pendingOrder", "overdueOrder", "deliveredOrder" })
        {
            var otherDrawer = await new GetSalesReportOrdersQueryHandler(db, saleUser, new Clock()).Handle(
                new() { Metric = metric, From = request.From, To = request.To, SalePerson = Guid.NewGuid() }, default);
            Assert.Equal(0, otherDrawer.Data!.TotalCount);
            Assert.Empty(otherDrawer.Data.Items);
        }
        var otherCustomers = await new GetSalesReportCustomersQueryHandler(db, saleUser, new Clock()).Handle(
            new() { Metric = "deliveredCustomer", From = request.From, To = request.To, SalePerson = Guid.NewGuid() }, default);
        Assert.Empty(otherCustomers.Data!.Items);
    }

    [Fact]
    public async Task Drawers_RejectOtherCompanyUnknownMetricAndInvalidPagination()
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = ["Developer"] };
        var orders = new GetSalesReportOrdersQueryHandler(db, user, new Clock());
        Assert.False((await orders.Handle(new() { Metric = "completedPo", CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.False((await orders.Handle(new() { Metric = "unknown" }, default)).Success);
        Assert.False((await orders.Handle(new() { Metric = "completedPo", PageSize = 101 }, default)).Success);
        Assert.False((await orders.Handle(new() { Metric = "completedPo", PageNumber = int.MaxValue }, default)).Success);
        Assert.False((await orders.Handle(new() { Metric = "pendingOrder", From = new(2026, 10, 1), To = new(2026, 9, 1) }, default)).Success);
        var customers = new GetSalesReportCustomersQueryHandler(db, user, new Clock());
        Assert.False((await customers.Handle(new() { Metric = "deliveredCustomer", CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.False((await customers.Handle(new() { Metric = "completedPo" }, default)).Success);
        Assert.False((await customers.Handle(new() { Metric = "deliveredCustomer", PageNumber = 0 }, default)).Success);
        Assert.False((await customers.Handle(new() { Metric = "deliveredCustomer", SaleGroup = Guid.Empty }, default)).Success);
    }

    [Fact]
    public void Header_ReconcilesRevenueCostAndMarginWithPnlSales_AndDoesNotRankComparisonMonth()
    {
        var date = new DateTime(2026, 9, 1);
        var lines = new[]
        {
            new DeliveryRevenueLine { CustomerId = Guid.NewGuid(), MerchandiseOrderId = Guid.NewGuid(),
                RevenueDate = date, Quantity = 2, RevenueAmountVnd = 200, BaseCostAmount = 80 },
            new DeliveryRevenueLine { CustomerId = Guid.NewGuid(), MerchandiseOrderId = Guid.NewGuid(),
                RevenueDate = date.AddDays(4), Quantity = 3, RevenueAmountVnd = 600, BaseCostAmount = 210 }
        };
        var sources = new DeliveryCostSources(new Dictionary<Guid, decimal>(),
            new Dictionary<Guid, decimal>(), new Dictionary<FormulaMonthKey, decimal>());
        var rows = lines.Select(x => new ExecutivePnLSalesMonthlyMetricRow
        {
            GroupKey = "group", GroupLabel = "Group", SaleKey = "sale", SaleLabel = "Sale",
            Year = 2026, Month = 9, Revenue = x.RevenueAmountVnd,
            CostOfSales = DeliveryCostResolver.ResolveAmount(x, sources)
        }).ToList();
        rows.Add(new() { SaleKey = "sale", SaleLabel = "Sale", GroupKey = "group", GroupLabel = "Group",
            Year = 2026, Month = 8, Revenue = 1000, CostOfSales = 400 });
        var header = SalesReportHeaderCalculator.Build(Guid.NewGuid(),
            new(date, date.AddMonths(1)), lines, 1000, [], new Dictionary<Guid, decimal>(), 0,
            rows.Where(x => x.Month == 9).Sum(x => x.CostOfSales), new DateTime(2026, 10, 9));
        var tab = ExecutivePnLDashboardBuilder.BuildSalesAnalysisTab([date], rows, 100);
        var total = tab.Sections.First().Lines.Single().MetricValues;
        Assert.Equal(total["revenue"]["actual_2026_09"], header.Revenue);
        Assert.Equal(total["costOfSales"]["actual_2026_09"], header.CostOfSales);
        Assert.Equal(total["profit"]["actual_2026_09"], header.Profit);
        Assert.Equal(total["marginPercent"]["actual_2026_09"], header.MarginPercent);
        Assert.Equal(800m, tab.Charts.First().Series.First().Data.Single());
        Assert.Equal(2, header.CustomerCount);
        Assert.Equal(2, header.DeliveredOrderCount);
    }

    [Fact]
    public void Pending_IsCalculatedPerLineAtEndOfPeriod_OverdeliveryDoesNotCancelAnotherLine()
    {
        var order = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var period = new SalesReportPeriod(new(2026, 9, 1), new(2026, 10, 1));
        var details = new[]
        {
            new SalesHeaderOrderLine { OrderId = order, DetailId = first, OrderDate = period.From,
                RequestedDate = period.From, Quantity = 10, UnitPrice = 2, TotalAmount = 20,
                Currency = "USD", ExchangeRate = 25000 },
            new SalesHeaderOrderLine { OrderId = order, DetailId = second, OrderDate = period.From,
                RequestedDate = period.From, Quantity = 10, UnitPrice = 2, TotalAmount = 20,
                Currency = "USD", ExchangeRate = 25000 }
        };
        var header = SalesReportHeaderCalculator.Build(Guid.NewGuid(), period, [], 0, details,
            new Dictionary<Guid, decimal> { [first] = 15, [second] = 3 }, 1, null, new DateTime(2026, 10, 9));
        Assert.Equal(7m, header.PendingQuantity);
        Assert.Equal(350000m, header.PendingAmount);
        Assert.Equal(350000m, header.ExpectedPendingAmountInPeriod);
        Assert.Equal(350000m, header.ExpectedRevenueInPeriod);
        Assert.Equal(1000000m, header.NewOrderAmount);
        Assert.Equal(1, header.PendingOrderCount);
        Assert.Equal(1, header.OverdueOrderCount);
        Assert.Null(header.RevenueGrowthPercent);
        Assert.Null(header.CostOfSales);
        Assert.Null(header.Profit);
        Assert.Null(header.MarginPercent);
        Assert.False(header.CanViewProfit);
    }

    [Fact]
    public void RevenueQuery_ExcludesCanceledInactiveInternalAndOtherCompany_AndUsesAgreedPrice()
    {
        var company = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var rows = Enumerable.Range(0, 6).Select(_ => new DeliveryOrderDetail
        {
            IsActive = true, Quantity = 2, MerchandiseOrderDetailId = Guid.NewGuid(),
            DeliveryOrder = new DeliveryOrder { IsActive = true, CompanyId = company,
                CreatedDate = from, CustomerExternalIdSnapShot = "CUSTOMER", Status = "New" },
            MerchandiseOrderDetail = new MerchandiseOrderDetail { UnitPriceAgreed = 5,
                MerchandiseOrder = new MerchandiseOrder { Currency = "USD", ExchangeRate = 25000 } }
        }).ToArray();
        rows[1].IsActive = false;
        rows[2].DeliveryOrder.Status = DeliveryOrderStatus.Canceled.ToString();
        rows[3].DeliveryOrder.CustomerExternalIdSnapShot = DeliveryRevenueQuery.InternalCustomerExternalId;
        rows[4].DeliveryOrder.CompanyId = Guid.NewGuid();
        rows[5].DeliveryOrder.CreatedDate = from.AddMonths(1);
        var result = Assert.Single(DeliveryRevenueQuery.Create(rows.AsQueryable(), from, from.AddMonths(1), company));
        Assert.Equal(250000m, result.RevenueAmountVnd);
    }

    [Fact]
    public void NonProfitProjection_TranslatesToPostgresWithoutCostOrLotTables()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
            new(2026, 9, 1), new(2026, 10, 1), Guid.NewGuid(), includeCosts: false).ToQueryString();
        Assert.DoesNotContain("TotalCostSnapshot", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BaseCostSnapshot", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LotConsumptions", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SlimRevenueProjection_KeepsCostSnapshotsWithoutUnusedDisplayJoins()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
            new(2026, 10, 1), new(2026, 11, 1), Guid.NewGuid(), includeDisplayFields: false).ToQueryString();
        Assert.Contains("TotalCostSnapshot", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BaseCostSnapshot", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("string_agg", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ColourCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProductTypeName", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("President", true)]
    [InlineData("ACCUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    [InlineData("Admin", false)]
    public void ProfitPermission_UsesCentralMapping(string role, bool allowed)
    {
        var user = new User { Roles = [role] };
        Assert.Equal(allowed, new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.Reports.ViewSalesProfit));
        Assert.False(new CurrentUserPermissionService(user).HasPermission("reports.unknown.view"));
        user.IsAuthenticated = false;
        Assert.False(new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.Reports.ViewSalesProfit));
    }

    [Fact]
    public void ExplicitPermissionSet_OverridesRoleFallback()
    {
        var withoutClaim = new User { Roles = ["President"], HasExplicitPermissionSet = true };
        Assert.False(new CurrentUserPermissionService(withoutClaim).HasPermission(ApplicationPermissions.Reports.ViewSalesProfit));
        var withClaim = new User { HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.Reports.ViewSalesProfit] };
        Assert.True(new CurrentUserPermissionService(withClaim).HasPermission(ApplicationPermissions.Reports.ViewSalesProfit));
    }

    [Fact]
    public async Task Handler_FailsClosedForOtherCompanyOrInvalidDates_AndMasksEmptySaleResult()
    {
        var (db, _) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = ["SaleUser"] };
        var handler = new GetSalesReportHeaderQueryHandler(db, user, new CurrentUserPermissionService(user), new Clock());
        Assert.False((await handler.Handle(new() { CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.False((await handler.Handle(new() { From = new(2026, 10, 1), To = new(2026, 9, 1) }, default)).Success);
        var result = await handler.Handle(new(), default);
        Assert.True(result.Success);
        Assert.Equal(new DateTime(2026, 9, 1), result.Data!.From);
        Assert.Null(result.Data.CostOfSales);
        Assert.Null(result.Data.Profit);
        Assert.False(ReportCompanyScope.IsAllowed(new User { CompanyId = null }, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReportCompanyScope.Resolve(user, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Developer", false, false, 400)]
    [InlineData("President", false, false, 400)]
    [InlineData("ACCUser", false, false, 400)]
    [InlineData("SaleUser", false, false, 100)]
    [InlineData("SaleAdmin", true, false, 200)]
    [InlineData("ACUser", false, true, 200)]
    public async Task ReportScope_UsesCustomerVisibility_ForBothEndpoints(string role, bool leader, bool claim, int expected)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var user = new User { CompanyId = company, EmployeeId = employee, Roles = [role] };
        var group = new Group { CompanyId = company, GroupId = Guid.NewGuid(), Name = "Team" };
        var otherGroup = new Group { CompanyId = company, GroupId = Guid.NewGuid(), Name = "Other" };
        var owners = new[] { employee, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var customers = owners.Select((owner, index) => new Customer
        {
            CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true, ExternalId = $"KH_{index}"
        }).ToArray();
        var assignments = customers.Select((customer, index) => new CustomerAssignment
        {
            CustomerId = customer.CustomerId, Customer = customer, EmployeeId = owners[index],
            CompanyId = company, GroupId = index < 2 ? group.GroupId : otherGroup.GroupId
        }).ToArray();
        foreach (var assignment in assignments) assignment.Customer.CustomerAssignments.Add(assignment);
        if (claim) customers[1].CustomerClaims.Add(new CustomerClaim
        {
            CustomerId = customers[1].CustomerId, CompanyId = company, EmployeeId = employee,
            GroupId = otherGroup.GroupId, ExpiresAt = DateTime.Now.AddDays(1)
        });
        store.Seed(nameof(db.CustomerAssignments), assignments);
        store.Seed(nameof(db.MemberInGroups), owners.Select((owner, index) => new MemberInGroup
        {
            Profile = owner, Group = index < 2 ? group : otherGroup,
            GroupId = index < 2 ? group.GroupId : otherGroup.GroupId, IsAdmin = index == 0 && leader
        }).ToArray());
        store.Seed(nameof(db.UserRoles), owners.Select(owner => new ApplicationUserRole
        {
            User = new ApplicationUser { EmployeeId = owner,
                Employee = new Employee { EmployeeId = owner, CompanyId = company, FullName = owner.ToString() } },
            Role = new ApplicationRole { Name = "SaleUser" }
        }).ToArray());
        var orders = customers.Select(customer => new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = company, CustomerId = customer.CustomerId,
            Customer = customer, CreateDate = new(2026, 9, 1)
        }).ToArray();
        store.Seed(nameof(db.MerchandiseOrders), orders);
        store.Seed(nameof(db.DeliveryOrderDetails), orders.Select(order => new DeliveryOrderDetail
        {
            Quantity = 1, MerchandiseOrderDetailId = Guid.NewGuid(),
            MerchandiseOrderDetail = new MerchandiseOrderDetail { MerchandiseOrder = order, UnitPriceAgreed = 100 },
            DeliveryOrder = new DeliveryOrder { CompanyId = company, CreatedDate = new(2026, 9, 20) }
        }).ToArray());
        var permissions = new CurrentUserPermissionService(user);
        var headerHandler = new GetSalesReportHeaderQueryHandler(db, user, permissions, new Clock());
        var header = await headerHandler.Handle(new(), default);
        Assert.Equal(expected, header.Data!.Revenue);
        var pnlHandler = new GetExecutivePnLDashboardBySalesQueryHandler(db, user, permissions);
        var pnl = await pnlHandler.Handle(new() { FromMonth = new(2026, 9, 1), ToMonth = new(2026, 9, 1) }, default);
        var total = pnl.Sections.Single(section => section.Code == "TOTAL_SYSTEM").Lines.Single();
        Assert.Equal(expected, total.MetricValues["revenue"]["actual_2026_09"]);
        var full = ApplicationRoleSets.Reports.SalesCompanyReaders.Contains(role);
        Assert.Equal(full, total.MetricValues.ContainsKey("profit"));
        if (!full)
        {
            Assert.All(pnl.Sections.SelectMany(section => section.Lines), line => Assert.Single(line.MetricValues));
            Assert.All(pnl.Charts.SelectMany(chart => chart.Series), series =>
                Assert.Contains(series.Key, new[] { "revenue", "revenueGrowthPercent" }));
        }
        Assert.False((await headerHandler.Handle(new() { CompanyId = Guid.NewGuid() }, default)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pnlHandler.Handle(new() { CompanyId = Guid.NewGuid() }, default));
        user.IsAuthenticated = false;
        Assert.False((await headerHandler.Handle(new(), default)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pnlHandler.Handle(new(), default));
    }

    [Fact]
    public async Task PromisedDate_UsesActiveCompanyProductionOrders_AndPendingDetailsOnly()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = ["Developer"] };
        var customer = new Customer { CustomerId = Guid.NewGuid(), ExternalId = "KH_TEST" };
        var order = new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            CustomerId = customer.CustomerId, Customer = customer, CreateDate = new(2026, 9, 1) };
        var details = Enumerable.Range(0, 2).Select(_ => new MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = order.MerchandiseOrderId,
            MerchandiseOrder = order, ExpectedQuantity = 10, DeliveryRequestDate = new(2026, 9, 10),
            ExpectedDeliveryDate = new(2026, 9, 2)
        }).ToArray();
        foreach (var detail in details) order.MerchandiseOrderDetails.Add(detail);
        store.Seed(nameof(db.MerchandiseOrders), order);
        store.Seed(nameof(db.MerchandiseOrderDetails), details);
        store.Seed(nameof(db.DeliveryOrderDetails), new DeliveryOrderDetail
        {
            Quantity = 10, MerchandiseOrderDetailId = details[0].MerchandiseOrderDetailId,
            MerchandiseOrderDetail = details[0], DeliveryOrder = new DeliveryOrder
                { CompanyId = user.CompanyId.Value, CreatedDate = new(2026, 9, 15) }
        });
        var production = Enumerable.Range(0, 5).Select(index => new HRM.Domain.Entities.ManufacturingSchema.MfgProductionOrder
        {
            MfgProductionOrderId = Guid.NewGuid(), CompanyId = index == 3 ? Guid.NewGuid() : user.CompanyId.Value,
            IsActive = index != 4, ExpectedDate = new DateTime(2026, 9, index < 2 ? 20 + index : 1)
        }).ToArray();
        var links = production.Select((row, index) => new HRM.Domain.Entities.ManufacturingSchema.MfgOrderPO
        {
            MfgProductionOrderId = row.MfgProductionOrderId,
            MerchandiseOrderDetailId = details[index == 0 ? 0 : 1].MerchandiseOrderDetailId,
            IsActive = index != 2
        }).ToArray();
        store.Seed(nameof(db.MfgProductionOrders), production);
        store.Seed(nameof(db.MfgOrderPOs), links);
        var handler = new GetSalesReportOrdersQueryHandler(db, user, new Clock());
        var pending = await handler.Handle(new() { Metric = "pendingOrder" }, default);
        Assert.Equal(new DateTime(2026, 9, 21), Assert.Single(pending.Data!.Items).PromisedDeliveryDate);
        var all = await handler.Handle(new() { Metric = "incompletePo" }, default);
        Assert.Equal(new DateTime(2026, 9, 20), Assert.Single(all.Data!.Items).PromisedDeliveryDate);
        store.Seed<HRM.Domain.Entities.ManufacturingSchema.MfgOrderPO>(nameof(db.MfgOrderPOs));
        var missing = await handler.Handle(new() { Metric = "pendingOrder" }, default);
        Assert.Null(Assert.Single(missing.Data!.Items).PromisedDeliveryDate);
    }

    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 9, 30); }
    [Theory]
    [InlineData("Developer")]
    [InlineData("President")]
    [InlineData("ACCUser")]
    public async Task CompanyTotals_IncludeUnassignedCustomers_ExcludeInternal_AndKeepSaleScope(string role)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var user = new User { Roles = [role] };
        var orders = new[] { "KH_PLPU", "KH_VIETAUS" }.Select(code =>
        {
            var customer = new Customer { CustomerId = Guid.NewGuid(), ExternalId = code, IsActive = true };
            return new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
                CustomerId = customer.CustomerId, Customer = customer, CustomerExternalIdSnapshot = code,
                CreateDate = new(2026, 9, 1), Status = "New" };
        }).ToArray();
        var details = orders.Select(o => new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(),
            MerchandiseOrderId = o.MerchandiseOrderId, MerchandiseOrder = o, ExpectedQuantity = 10,
            DeliveryRequestDate = new(2026, 9, 15), UnitPriceAgreed = 100, TotalPriceAgreed = 1000 }).ToArray();
        store.Seed(nameof(db.MerchandiseOrders), orders);
        store.Seed(nameof(db.MerchandiseOrderDetails), details);
        store.Seed(nameof(db.DeliveryOrderDetails), details.Select(d => new DeliveryOrderDetail
        {
            MerchandiseOrderDetailId = d.MerchandiseOrderDetailId, MerchandiseOrderDetail = d, Quantity = 3,
            DeliveryOrder = new DeliveryOrder { CompanyId = user.CompanyId!.Value, CreatedDate = new(2026, 9, 20),
                CustomerExternalIdSnapShot = "legacy-snapshot" }
        }).ToArray());
        var header = await new GetSalesReportHeaderQueryHandler(db, user,
            new CurrentUserPermissionService(user), new Clock()).Handle(new(), default);
        Assert.Equal(300m, header.Data!.Revenue);
        Assert.Equal(1, header.Data.CustomerCount);
        Assert.Equal(1, header.Data.PendingOrderCount);
        foreach (var metric in new[] { "incompletePo", "pendingOrder", "overdueOrder", "deliveredOrder" })
        {
            var drawer = await new GetSalesReportOrdersQueryHandler(db, user, new Clock()).Handle(new() { Metric = metric }, default);
            Assert.Equal(1, drawer.Data!.TotalCount);
        }
        var customers = await new GetSalesReportCustomersQueryHandler(db, user, new Clock()).Handle(
            new() { Metric = "deliveredCustomer" }, default);
        Assert.Equal(1, customers.Data!.TotalCount);
        if (role != "ACCUser")
        {
            var body = await new GetSalesReportBodyQueryHandler(db, user,
                new CurrentUserPermissionService(user), new Clock()).Handle(new(), default);
            Assert.Equal(header.Data.Revenue, body.Data!.Totals.Revenue);
            Assert.Equal("UNASSIGNED", Assert.Single(body.Data.Groups).Key);
        }
        foreach (var reader in new[] { user, new User { CompanyId = user.CompanyId, Roles = ["SaleUser"] } })
        {
            var scoped = await new GetSalesReportHeaderQueryHandler(db, reader,
                new CurrentUserPermissionService(reader), new Clock()).Handle(new() { SalePerson = Guid.NewGuid() }, default);
            Assert.Equal(0m, scoped.Data!.Revenue);
        }
        var sale = new User { CompanyId = user.CompanyId, Roles = ["SaleUser"] };
        var saleHeader = await new GetSalesReportHeaderQueryHandler(db, sale,
            new CurrentUserPermissionService(sale), new Clock()).Handle(new(), default);
        Assert.Equal(0m, saleHeader.Data!.Revenue);
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
