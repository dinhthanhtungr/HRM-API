using ClosedXML.Excel;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.Deliverers.Commands.CreateDeliverer;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.DeleteDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.FinishDelivery;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PrintDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetTransportSummary;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Excels;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryLegacyParityTests
{
    [Theory]
    [InlineData("SaleUser", true, true)]
    [InlineData("DispatchUser", false, true)]
    [InlineData("DispatchUser", true, false)]
    public async Task TransportJson_DeniesBeforeReading(string role, bool authenticated, bool hasCompany)
    {
        var user = new User(role, authenticated, hasCompany ? Guid.NewGuid() : null);
        var handler = new GetTransportSummaryQueryHandler(null!, user);
        Assert.False((await handler.Handle(new(DateTime.Today, DateTime.Today), default)).Success);
    }

    [Fact]
    public async Task TransportJson_RejectsInvalidDatesBeforeReading()
    {
        var user = new User("DispatchUser", CompanyId: Guid.NewGuid());
        var handler = new GetTransportSummaryQueryHandler(null!, user);
        Assert.False((await handler.Handle(new(default, DateTime.Today), default)).Success);
        Assert.False((await handler.Handle(new(DateTime.Today, DateTime.Today.AddDays(-1)), default)).Success);
        Assert.False((await handler.Handle(new(DateTime.Today, DateTime.MaxValue), default)).Success);
    }

    [Fact]
    public void PlanExcel_RequiresPromiseDateButDoesNotRequireDeliveryOrder()
    {
        var company = Guid.NewGuid();
        var date = new DateTime(2026, 10, 6);
        MfgOrderPO Link(DateTime? promise) => new()
        {
            IsActive = true,
            Detail = new MerchandiseOrderDetail
            {
                IsActive = true, ExpectedQuantity = 125,
                MerchandiseOrder = new MerchandiseOrder { CompanyId = company, IsActive = true }
            },
            ProductionOrder = new MfgProductionOrder
            {
                CompanyId = company, IsActive = true, Status = "New", ExpectedDate = promise
            }
        };
        var result = DeliveryPlanReportQuery.Project(new[] { Link(null), Link(date), Link(date.AddDays(-1)) }.AsQueryable(),
            company, date, date).ToArray();
        var row = Assert.Single(result);
        Assert.Equal(date, row.ExpectedDate);
        Assert.Equal(125m, row.Requested);
        Assert.Equal(0m, row.Allocated);
    }

    private sealed record User(string Role, bool IsAuthenticated = true, Guid? CompanyId = null, Guid? EmployeeId = null) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => Role == role;
    }

    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("DispatchUser", false)]
    [InlineData("Guest", true)]
    public async Task Mutations_DenyBeforeReading(string role, bool authenticated)
    {
        var user = new User(role, authenticated, Guid.NewGuid(), Guid.NewGuid());
        var id = Guid.NewGuid();
        Assert.False((await new PrintDeliveryOrderCommandHandler(null!, null!, user, null!, null!).Handle(new(id), default)).Success);
        Assert.False((await new DeleteDeliveryOrderCommandHandler(null!, user, null!).Handle(new(id), default)).Success);
        Assert.False((await new PatchDeliveryOrderCommandHandler(null!, user, null!, null!).Handle(new() { Id = id }, default)).Success);
        if (!DeliveryOrderAccessRules.CanFinish(user))
            Assert.False((await new FinishDeliveryCommandHandler(null!, user, null!, null!, null!).Handle(new(id), default)).Success);
    }

    [Fact]
    public async Task MissingScope_DeniesMutationAndReports()
    {
        var user = new User("DispatchUser");
        Assert.False((await new PrintDeliveryOrderCommandHandler(null!, null!, user, null!, null!).Handle(new(Guid.NewGuid()), default)).Success);
        Assert.False((await new ExportDeliveryReportQueryHandler(null!, user, null!).Handle(new(DateTime.Today, DateTime.Today, DeliveryReportKind.Plan), default)).Success);
    }

    [Fact]
    public async Task Transport_DeniesReadOnlyRole_AndSharedCatalogRequiresAdmin()
    {
        var user = new User("SaleUser", CompanyId: Guid.NewGuid(), EmployeeId: Guid.NewGuid());
        Assert.False((await new ExportDeliveryReportQueryHandler(null!, user, null!).Handle(new(DateTime.Today, DateTime.Today, DeliveryReportKind.Transport), default)).Success);
        Assert.False((await new CreateDelivererCommandHandler(null!, user).Handle(new("Driver", null, null, null), default)).Success);
        Assert.False((await new CreateDelivererCommandHandler(null!, new User("Admin")).Handle(new(" ", null, null, null), default)).Success);
    }

    [Fact]
    public async Task InvalidDates_AndDuplicateBagIds_DenyBeforeReading()
    {
        var user = new User("DispatchUser", CompanyId: Guid.NewGuid(), EmployeeId: Guid.NewGuid());
        var reports = new ExportDeliveryReportQueryHandler(null!, user, null!);
        Assert.False((await reports.Handle(new(DateTime.Today.AddDays(1), DateTime.Today, DeliveryReportKind.Plan), default)).Success);
        Assert.False((await reports.Handle(new(DateTime.Today, DateTime.MaxValue, DeliveryReportKind.Plan), default)).Success);
        var id = Guid.NewGuid();
        Assert.False((await new PatchDeliveryOrderCommandHandler(null!, user, null!, null!).Handle(new()
        {
            Id = Guid.NewGuid(), Details = [new(id, 1), new(id, 2)]
        }, default)).Success);
    }

    [Fact]
    public void Reports_ProjectCompanyScope_WithoutCostColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var from = new DateTime(2026, 1, 1);
        var to = from.AddDays(2);
        foreach (var sql in new[]
        {
            DeliveryPlanReportQuery.Project(db.MfgOrderPOs.AsNoTracking(), company, from, to).ToQueryString(),
            DeliveryFinishReportQuery.Project(db.DeliveryOrders.AsNoTracking(), company, from, to).ToQueryString(),
            DeliveryTransportReportQuery.Project(db.DeliveryOrders.AsNoTracking(), company, from, to).ToQueryString()
        })
        {
            Assert.Contains("CompanyId", sql);
            Assert.DoesNotContain("UnitCostSnapshot", sql);
            Assert.DoesNotContain("TotalCostSnapshot", sql);
            Assert.DoesNotContain("UnitPriceAgreed", sql);
        }
    }

    [Fact]
    public void FinishAndTransport_ExcludeOtherCompanyAndInactiveHeaders()
    {
        var company = Guid.NewGuid();
        var date = new DateTime(2026, 1, 1);
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = company, CreatedDate = date,
            Company = new() { Name = "Company" }, Details = [new() { Quantity = 5, IsActive = true }] };
        var source = new[] { order }.AsQueryable();
        Assert.Single(DeliveryFinishReportQuery.Project(source, company, date, date));
        Assert.Single(DeliveryTransportReportQuery.Project(source, company, date, date));
        Assert.Empty(DeliveryFinishReportQuery.Project(source, Guid.NewGuid(), date, date));
        Assert.Empty(DeliveryTransportReportQuery.Project(source, Guid.NewGuid(), date, date));
        order.IsActive = false;
        Assert.Empty(DeliveryFinishReportQuery.Project(source, company, date, date));
        Assert.Empty(DeliveryTransportReportQuery.Project(source, company, date, date));
    }

    [Fact]
    public void TransportWorkbook_MatchesSummaryAndDetailsReturnedToClient()
    {
        var day = new DateTime(2026, 10, 1);
        var data = DeliveryTransportReportQuery.Build([new()
        {
            Id = Guid.NewGuid(), ExternalId = "GH001", Date = day, CompanyName = "Tenant",
            Price = 150000, Deliverers = ["A", "B"],
            Lines = [new() { Quantity = 100, Bags = 4, Code = "P1", Name = "Product", Lot = "L1", Po = "PO1" }]
        }]);
        using var workbook = new XLWorkbook(new MemoryStream(new DeliveryReportExcelRenderer().ExportTransportWorkbook(data, day, day)));
        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.Equal("ĐƠN VỊ: Tenant", workbook.Worksheet("Tổng kết").Cell(1, 1).GetString());
        foreach (var summary in data.SummaryRows)
        {
            var summaryRow = 6 + summary.Stt;
            Assert.Equal(summary.TotalAmount, workbook.Worksheet("Tổng kết").Cell(summaryRow, 5).GetValue<decimal>());
            Assert.Equal(summary.RemainingAmount, workbook.Worksheet("Tổng kết").Cell(summaryRow, 7).GetValue<decimal>());
            var detail = Assert.Single(data.DetailByDeliverer[summary.DelivererName]);
            var sheet = workbook.Worksheet(summary.DelivererName);
            Assert.Equal(detail.DeliveryExternalId, sheet.Cell(6, 3).GetString());
            Assert.Equal(detail.TotalQuantity, sheet.Cell(6, 7).GetValue<decimal>());
            Assert.Equal(detail.TotalBags, sheet.Cell(6, 8).GetValue<int>());
            Assert.Equal(detail.DeliveryPrice, sheet.Cell(6, 11).GetValue<decimal>());
            Assert.Equal(detail.DeliveryPrice, sheet.Cell(7, 11).GetValue<decimal>());
        }
        var empty = DeliveryTransportReportQuery.Build([]);
        Assert.Empty(empty.SummaryRows);
        Assert.Empty(empty.DetailByDeliverer);
        using var emptyWorkbook = new XLWorkbook(new MemoryStream(new DeliveryReportExcelRenderer().ExportTransportWorkbook(empty, day, day)));
        Assert.Equal("Tổng kết", Assert.Single(emptyWorkbook.Worksheets).Name);
    }

    [Fact]
    public void Transport_SplitsFeePerDistinctDriver_PreservesLegacyQuantityPerDriver()
    {
        var result = DeliveryTransportReportQuery.Build([new()
        {
            Id = Guid.NewGuid(), Price = 120m, Deliverers = ["A", "a", "B"],
            Lines = [new() { Quantity = 10, Bags = 2 }, new() { Quantity = 5, Bags = 1 }]
        }]);
        Assert.Equal(2, result.SummaryRows.Count);
        Assert.All(result.SummaryRows, row => { Assert.Equal(1, row.TotalTrips); Assert.Equal(15, row.TotalQuantity); Assert.Equal(60, row.TotalAmount); });
        Assert.Equal(120, result.SummaryRows.Sum(x => x.TotalAmount));
        var unassigned = DeliveryTransportReportQuery.Build([new() { Price = 50, Lines = [new() { Quantity = 1 }] }]);
        Assert.Equal(50, Assert.Single(unassigned.DetailByDeliverer[DeliveryTransportReportQuery.UnassignedDeliverer]).DeliveryPrice);
    }

    [Fact]
    public void WarehouseRequest_GroupsByProductAndNormalizedLots_WithoutMergingDifferentLots()
    {
        var rows = DeliveryWarehouseRequestService.BuildDetails([
            new() { ProductExternalIdSnapShot = "P", LotNoList = "A; A", Quantity = 10, NumOfBags = 1 },
            new() { ProductExternalIdSnapShot = "P", LotNoList = "A", Quantity = 5, NumOfBags = 2 },
            new() { ProductExternalIdSnapShot = "P", LotNoList = "B", Quantity = 3 },
            new() { ProductExternalIdSnapShot = "P", LotNoList = "A", Quantity = 100, IsActive = false }
        ]);
        Assert.Equal(2, rows.Count);
        Assert.Equal(15, rows.Single(x => x.LotNumber == "A").WeightKg);
        Assert.Equal(3, rows.Single(x => x.LotNumber == "A").BagNumber);
    }

    [Fact]
    public void LegacyExcelTemplates_ProduceWorkbooks_AndAvoidDriverSheetCollisions()
    {
        var renderer = new DeliveryReportExcelRenderer();
        var day = new DateTime(2026, 1, 1);
        using var plan = new XLWorkbook(new MemoryStream(renderer.ExportDeliveryPlanExcel([new() { CustomerName = "Customer", QuantityText = "5*", QuantityIsPending = true }])));
        Assert.Single(plan.Worksheets);
        using var finish = new XLWorkbook(new MemoryStream(renderer.ExportDeliveryFinish([new() { QuantityKg = 5, NumOfBags = 1 }], day, day)));
        Assert.Single(finish.Worksheets);
        var data = new DeliveryTransportWorkbookData { CompanyName = "Tenant name", DetailByDeliverer = new()
        {
            ["Tổng kết"] = [new() { DelivererName = "Tổng kết", DeliveryPrice = 5 }],
            ["A/B"] = [new() { DelivererName = "A/B", DeliveryPrice = 5 }],
            ["AB"] = [new() { DelivererName = "AB", DeliveryPrice = 5 }]
        }};
        using var transport = new XLWorkbook(new MemoryStream(renderer.ExportTransportWorkbook(data, day, day)));
        Assert.Equal(4, transport.Worksheets.Count);
        Assert.Contains("Tenant name", transport.Worksheet("Tổng kết").Cell(1, 1).GetString());
    }
}
