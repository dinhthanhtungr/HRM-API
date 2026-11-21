using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using HRM.Infrastructure.Documents.Excels;

namespace HRM.Application.Tests.Features.ExcelSpecial;

public sealed class ManufacturingVaExportTests
{
    [Theory]
    [InlineData(ItemType.Material, true)]
    [InlineData(ItemType.MaterialFailure, true)]
    [InlineData(ItemType.Product, false)]
    [InlineData(ItemType.ProductFailure, false)]
    public void SingleMaterial_OverridesGrinding_WhileSingleProductDoesNot(ItemType type, bool trade)
    {
        var va = Guid.NewGuid(); var product = type is ItemType.Product or ItemType.ProductFailure;
        var row = Assert.Single(ManufacturingVaExportMapper.Map([new(va, "VA-1", new(2026, 11, 1))],
            [new(va, 1, Guid.NewGuid(), type, product ? null : Guid.NewGuid(), product ? Guid.NewGuid() : null, "ITEM", "Name", "Kg", 1)],
            [Selection(va, Guid.NewGuid(), null, StepOfProduct.BN_GH)], [], [], [], default));
        Assert.Equal(trade, row.IsSingleMaterial);
        Assert.Equal(trade ? "TM" : "SX", row.ProductionType);
        Assert.StartsWith(trade ? "TH1" : "TH6", row.Case);
    }

    [Fact]
    public void Mapper_DeduplicatesSelection_UsesOrderCustomer_KeepsMissingReceiptBlank()
    {
        var va = Guid.NewGuid(); var mfg = Guid.NewGuid();
        var current = Selection(va, mfg, null, StepOfProduct.MD_GH);
        var row = Assert.Single(ManufacturingVaExportMapper.Map([new(va, "VA-1", new(2026, 11, 1))], [],
            [current with { ValidTo = new(2026, 10, 1) }, current, current],
            [new(mfg, "DHG-1", "KH_123", OrderType.Merchandise), new(mfg, "DHG-1", "KH_123", OrderType.Merchandise)],
            [new(current.ProductId, "TP_001")], [], default));
        Assert.Equal("MFG-1", row.MfgCode);
        Assert.Equal("KH_123", row.CustomerCodes);
        Assert.Equal("DHG-1", row.OrderCodes);
        Assert.Equal("Đơn bán cho khách", row.Output);
        Assert.Equal("TP_001", row.ProductExternalId);
        Assert.Null(row.LastReceiptDate);
        Assert.Null(row.ReceivedQuantity);
        Assert.Equal(125m, row.PlannedQuantity);
    }

    [Theory]
    [InlineData(OrderType.Internal, "KH_1", "Nội bộ")]
    [InlineData(OrderType.Merchandise, " kh_vietaus ", "Nội bộ")]
    [InlineData(OrderType.SampleRequest, "KH_1", "Đơn mẫu cho khách")]
    [InlineData(OrderType.Merchandise, "KH_1", "Đơn bán cho khách")]
    [InlineData(OrderType.Complaint, "KH_1", "Đơn khiếu nại - cần xác nhận")]
    public void Output_UsesOrderTypeAndInternalCustomer(OrderType type, string customer, string output)
    {
        var va = Guid.NewGuid();var mfg = Guid.NewGuid();
        var row = Assert.Single(ManufacturingVaExportMapper.Map([new(va, "VA-1", new(2026, 11, 1))], [],
            [Selection(va, mfg, null, null)], [new(mfg, "DHG", customer, type)], [], [], default));
        Assert.Equal(output, row.Output);
    }

    [Fact]
    public void MultipleDifferentMfg_DoesNotSelectArbitraryPlan_ReceiptRemainsPerVa()
    {
        var va = Guid.NewGuid(); var date = new DateTime(2026, 11, 16);
        var row = Assert.Single(ManufacturingVaExportMapper.Map([new(va, "VA-1", date)], [],
            [Selection(va, Guid.NewGuid(), null, null), Selection(va, Guid.NewGuid(), null, null)], [], [],
            [new("VA-1", 70m, date, ["COLOUR"])], default));
        Assert.Empty(row.MfgCode);
        Assert.Null(row.PlannedQuantity);
        Assert.Equal(70m, row.ReceivedQuantity);
        Assert.Equal(date, row.LastReceiptDate);
        Assert.Contains("Không có lệnh MFG duy nhất", row.Notes);
    }

    [Theory]
    [InlineData("no-material-permission")]
    [InlineData("no-order-permission")]
    [InlineData("company")]
    [InlineData("anonymous")]
    public async Task Handler_DeniesBeforeAnyRead(string scenario)
    {
        var user = new IqcWorkflowTests.User {
            HasExplicitPermissionSet = true, IsAuthenticated = scenario != "anonymous",
            CompanyId = scenario == "company" ? null : Guid.NewGuid(),
            Permissions = scenario == "no-order-permission" ? [] : [ApplicationPermissions.PLM.ViewProductionOrders]
        };
        var renderer = new CaptureRenderer();
        var handler = new ExportManufacturingVaQueryHandler(null!, null!, user,
            new CurrentUserPermissionService(user), new Visibility(scenario != "no-material-permission"), renderer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(), default));
        Assert.Null(renderer.Rows);
    }

    [Theory]
    [InlineData("range")]
    [InlineData("end")]
    [InlineData("id")]
    public async Task Handler_RejectsInvalidFilterBeforeAnyRead(string scenario)
    {
        var user = AuthorizedUser();
        var handler = new ExportManufacturingVaQueryHandler(null!, null!, user,
            new CurrentUserPermissionService(user), new Visibility(true), new CaptureRenderer());
        var query = scenario switch {
            "range" => new ExportManufacturingVaQuery { From = new(2026, 11, 2), To = new(2026, 11, 1) },
            "end" => new ExportManufacturingVaQuery { To = DateTime.MaxValue },
            _ => new ExportManufacturingVaQuery { ManufacturingFormulaIds = [Guid.Empty] }
        };
        Assert.False((await handler.Handle(query, default)).Success);
    }

    [Fact]
    public void MissingProductionQuantity_KeepsPlanAndRequiredMaterialBlank()
    {
        var va = Guid.NewGuid();
        var row = Assert.Single(ManufacturingVaExportMapper.Map([new(va, "VA-1", new(2026, 11, 1))],
            [new(va, 1, Guid.NewGuid(), ItemType.Material, Guid.NewGuid(), null, "NVL", "Name", "Kg", 0.2m)],
            [Selection(va, Guid.NewGuid(), null, null) with { PlannedQuantity = null }], [], [], [], default));
        Assert.Null(row.PlannedQuantity);
        Assert.Contains("chưa có khối lượng sản xuất", row.Notes);
        using var stream = new MemoryStream(new ManufacturingVaExcelRenderer().Render([row]));
        using var workbook = new XLWorkbook(stream);
        Assert.True(workbook.Worksheet("VA tổng hợp").Cell(2, 15).IsEmpty());
        Assert.True(workbook.Worksheet("Chi tiết theo mẫu").Cell(2, 15).IsEmpty());
        Assert.True(workbook.Worksheet("Chi tiết theo mẫu").Cell(2, 24).IsEmpty());
    }

    [Fact]
    public void Workbook_HasTemplateColumns_TypedDatesAndAmounts_NoFormulaInjection_AndSummaryDoesNotMultiply()
    {
        var row = new ManufacturingVaExcelRow("VA-1", new(2026, 11, 1), new(2026, 11, 16), "DHG", "KH", "MFG",
            "TH3", "SX", "Đơn bán cho khách", "Finished", "TP_001", "001", "=1+1", 100m, 70m, false,
            "Đùn", "Compound", "", [new("NVL-1", "First", 0.2m), new("NVL-2", "Second", 0.8m)]);
        var renderer = new ManufacturingVaExcelRenderer();
        using var stream = new MemoryStream(renderer.Render([row]));
        using var book = new XLWorkbook(stream);
        var summary = book.Worksheet("VA tổng hợp");var detail = book.Worksheet("Chi tiết theo mẫu");
        Assert.Equal(4, book.Worksheets.Count);
        Assert.Equal(28, detail.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("Mã công trình", detail.Cell(1, 28).GetString());
        Assert.Equal(2, summary.LastRowUsed()!.RowNumber());
        Assert.Equal(3, detail.LastRowUsed()!.RowNumber());
        Assert.Equal(70m, summary.Cell(2, 16).GetValue<decimal>());
        Assert.Equal(20m, detail.Cell(2, 24).GetValue<decimal>());
        Assert.Equal(new DateTime(2026, 11, 1), detail.Cell(2, 1).GetDateTime());
        Assert.Equal("001", detail.Cell(2, 12).GetString());
        Assert.Equal("=1+1", detail.Cell(2, 13).GetString());
        Assert.False(detail.Cell(2, 13).HasFormula);
        using var emptyStream = new MemoryStream(renderer.Render([]));
        using var empty = new XLWorkbook(emptyStream);
        Assert.Equal(1, empty.Worksheet("Chi tiết theo mẫu").LastRowUsed()!.RowNumber());
    }

    internal static IqcWorkflowTests.User AuthorizedUser() => new() {
        HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.PLM.ViewProductionOrders]
    };
    private static VaSelection Selection(Guid va, Guid mfg, DateTime? validTo, StepOfProduct? step) =>
        new(va, mfg, validTo, "MFG-1", "Finished", 125m, step, Guid.NewGuid(), "COLOUR", "Product", "CMP", "Compound");
    internal sealed class Visibility(bool materials) : IPLMFieldVisibilityService
    {
        public bool CanViewFormulaMaterials() => materials;
        public bool CanViewFormulaPrices() => false;
        public bool CanViewProductTechnicalInfo() => false;
        public bool CanViewMaterialPriceReviewDetails() => false;
    }
    internal sealed class CaptureRenderer : IManufacturingVaExcelRenderer
    {
        public IReadOnlyList<ManufacturingVaExcelRow>? Rows;
        public byte[] Render(IReadOnlyList<ManufacturingVaExcelRow> rows, CancellationToken ct = default) { Rows = rows; return [1]; }
    }
}
