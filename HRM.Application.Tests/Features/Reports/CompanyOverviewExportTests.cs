using ClosedXML.Excel;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Infrastructure.Documents.Excels;
using Xunit;

namespace HRM.Application.Tests.Features.Reports;

public sealed class CompanyOverviewExportTests
{
    [Fact]
    public void Workbook_HasExactlyThreeSheets_AndPreservesNumericNullZeroAndLiteralText()
    {
        var supplies = new CompanyPurchaseFact[]
        {
            new() { Source = "PurchaseOrder", Kind = "Supplies", DocumentCode = "=1+1", ItemCode = "VT1", Date = new(2026, 11, 1), Amount = 0m },
            new() { Source = "PurchaseOrder", Kind = "Supplies", DocumentCode = "PO2", ItemCode = "VT2", Date = new(2026, 11, 2), Amount = null, Issue = "MissingOrderedAmount" }
        };
        var report = Report(CompanyPurchaseCalculator.Build(supplies));
        var sales = new[] { new CompanySalesExportRow(new(2026, 11, 1), "DHG261100001", "=HYPERLINK(\"x\")", "P1", "Sản phẩm", 1250m, 1234.56m) };
        var bytes = new CompanyOverviewExcelRenderer().Render(new(report, sales, [], supplies));
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(new[] { "Doanh số", "NVL", "Vật tư" }, workbook.Worksheets.Select(x => x.Name));
        Assert.Equal("Mã DHG", workbook.Worksheet("Doanh số").Cell(10, 2).GetString());
        Assert.Equal("DHG261100001", workbook.Worksheet("Doanh số").Cell(11, 2).GetString());
        Assert.Equal(1.25m, workbook.Worksheet("Doanh số").Cell(11, 6).GetValue<decimal>());
        Assert.Equal(1234.56m, workbook.Worksheet("Doanh số").Cell(11, 7).GetValue<decimal>());
        Assert.Equal("=HYPERLINK(\"x\")", workbook.Worksheet("Doanh số").Cell(11, 3).GetString());
        Assert.Equal("=1+1", workbook.Worksheet("Vật tư").Cell(11, 2).GetString());
        Assert.Equal(XLDataType.Number, workbook.Worksheet("Vật tư").Cell(11, 5).DataType);
        Assert.Equal(0m, workbook.Worksheet("Vật tư").Cell(11, 5).GetValue<decimal>());
        Assert.True(workbook.Worksheet("Vật tư").Cell(12, 5).IsEmpty());
        Assert.Contains("Thiếu tiền", workbook.Worksheet("Vật tư").Cell(12, 6).GetString());
        Assert.DoesNotContain(workbook.Worksheets.SelectMany(x => x.CellsUsed()), x => x.HasFormula);
        Assert.DoesNotContain(workbook.Worksheets.SelectMany(x => x.CellsUsed()), x => x.GetString().Contains("BaseCostSnapshot"));
    }

    [Fact]
    public async Task RawMaterialExport_IgnoresUiPagination_AndMatchesExistingPlpuSummary()
    {
        var raw = new PLPUPurchaseReportRawData
        {
            OrderLines = Enumerable.Range(1, 135).Select(i => new PLPUPurchaseOrderLineRaw
            {
                PurchaseOrderId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PurchaseOrderDetailId = Guid.NewGuid(), POExternalId = "NCC1", LineNo = i,
                MaterialCode = "NVL_" + i, OrderedQuantity = 2m, UnitPriceAgreed = 3m,
                POIsActive = true, CreatedDate = new(2026, 11, 1)
            }).ToList()
        };
        var service = new PLPUPurchaseOverviewReportService(new RawRepository(raw));
        var query = new PLPUPurchaseOverviewQuery { CompanyId = Guid.NewGuid(), From = new(2026, 11, 1), To = new(2026, 11, 12), PageSize = 1 };
        var summary = await service.GetSummaryAsync(query);
        var rows = await service.GetExportLinesAsync(query);
        Assert.Equal(135, rows.Count);
        Assert.Equal(summary.TotalPurchaseValue, rows.Sum(x => x.TotalPriceAgreed ?? x.OrderedQuantity * (x.UnitPriceAgreed ?? 0m)));
        Assert.Equal(summary.ReceivedPurchaseValue, rows.Sum(x => x.ReceivedPurchaseValue));
    }

    [Theory]
    [InlineData("Sale", false, false)]
    [InlineData("President", true, false)]
    [InlineData("President", false, true)]
    public async Task Export_RejectsForbiddenCompanyOrMissingCapabilityBeforeReading(string role, bool otherCompany, bool explicitEmpty)
    {
        var user = new User(role, explicitEmpty);
        var handler = new ExportCompanyOverviewQueryHandler(user, new CurrentUserPermissionService(user),
            null!, null!, null!, null!, null!, null!);
        var result = await handler.Handle(new ExportCompanyOverviewQuery
        { CompanyId = otherCompany ? Guid.NewGuid() : user.CompanyId }, CancellationToken.None);
        Assert.False(result.Success);
    }

    private static CompanyOverviewDto Report(CompanyPurchasesDto purchases) => new()
    {
        CompanyId = Guid.NewGuid(), From = new(2026, 11, 1), To = new(2026, 11, 12), UpdatedAt = new(2026, 11, 12, 10, 0, 0),
        Sales = new SalesReportHeaderDto { Revenue = 1234.56m, DeliveredQuantity = 1250m },
        RawMaterials = new PLPUPurchaseOverviewSummaryDto(), Purchases = purchases
    };

    private sealed class RawRepository(PLPUPurchaseReportRawData raw) : IPLPUPurchaseOverviewReportRepository
    {
        public Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct) => Task.FromResult(raw);
    }

    private sealed class User(string role, bool explicitEmpty) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet => explicitEmpty;
        public bool IsInRole(string value) => value == role;
    }
}
