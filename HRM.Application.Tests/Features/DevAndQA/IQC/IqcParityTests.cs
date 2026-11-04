using ClosedXML.Excel;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.Documents.Excels.IQC;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcParityTests
{
    [Fact]
    public void Paging_PreservesLegacyPageField()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new IqcPagedResult<GetSummaryQCInput>([], 35, 2, 15),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("totalPages").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("pageNumber", out _));
    }

    [Fact]
    public void Patch_OmittedFieldsRemainAndFalseIsApplied()
    {
        var qc = new QCInputByQC
        {
            InspectionMethod = "Lab", IsCOAProvided = true, IsMSDSTDSProvided = true,
            IsSuccessQuality = true, ImportWarehouseType = QcDecision.Special, Note = "Stored"
        };
        var request = new PatchQCInputByQC { IsCOAProvided = false };
        Assert.Null(IqcMutation.Validate(request));
        IqcMutation.Apply(qc, request);
        Assert.False(qc.IsCOAProvided);
        Assert.True(qc.IsMSDSTDSProvided);
        Assert.True(qc.IsSuccessQuality);
        Assert.Equal(QcDecision.Special, qc.ImportWarehouseType);
        Assert.Equal("Lab", qc.InspectionMethod);
        Assert.Equal("Stored", qc.Note);
    }

    [Fact]
    public void Patch_ClearIsExplicitWhitelistedAndCannotConflict()
    {
        var request = new PatchQCInputByQC { ClearFields = ["note", "isCOAProvided", "importWarehouseType"] };
        Assert.Null(IqcMutation.Validate(request));
        var qc = new QCInputByQC { Note = "stored", IsCOAProvided = true, ImportWarehouseType = QcDecision.QCPass };
        IqcMutation.Apply(qc, request);
        Assert.Null(qc.Note);
        Assert.Null(qc.IsCOAProvided);
        Assert.Null(qc.ImportWarehouseType);
        Assert.NotNull(IqcMutation.Validate(new() { ClearFields = ["createdBy"] }));
        Assert.NotNull(IqcMutation.Validate(new() { Note = "value", ClearFields = ["note"] }));
        Assert.NotNull(IqcMutation.Validate(new() { ImportWarehouseType = (QcDecision)99 }));
    }

    [Theory]
    [InlineData(QcDecision.QCPass, VoucherDetailType.QCPass, true)]
    [InlineData(QcDecision.Special, VoucherDetailType.Special, true)]
    [InlineData(QcDecision.QCFail, VoucherDetailType.QCFail, false)]
    [InlineData(QcDecision.Waiter, VoucherDetailType.Waiter, false)]
    public void DecisionsMatchLegacy(QcDecision decision, VoucherDetailType voucherType, bool accepted)
    {
        Assert.Equal(voucherType, IqcRules.ToVoucherType(decision));
        Assert.Equal(accepted, IqcRules.IsAccepted(decision));
    }

    [Fact]
    public void PoStatus_UsesOnlyActiveLinesAndLegacyZeroQuantityRule()
    {
        var a = new PurchaseOrderDetail { IsActive = true, RequestQuantity = 10, RealQuantity = 0 };
        var b = new PurchaseOrderDetail { IsActive = true, RequestQuantity = 20, RealQuantity = 0 };
        var inactive = new PurchaseOrderDetail { IsActive = false, RequestQuantity = 100, RealQuantity = 0 };
        Assert.Equal("Pending", IqcRules.PurchaseOrderStatus([a, b, inactive]));
        a.RealQuantity = 5;
        Assert.Equal("InProgress", IqcRules.PurchaseOrderStatus([a, b, inactive]));
        a.RealQuantity = 12;
        b.RealQuantity = 20;
        Assert.Equal("Completed", IqcRules.PurchaseOrderStatus([a, b, inactive]));
        Assert.Null(IqcRules.PurchaseOrderStatus([inactive]));
        Assert.Equal("Completed", IqcRules.PurchaseOrderStatus([new() { IsActive = true }]));
    }

    [Fact]
    public void Reports_CountDistinctCodesPerGroupAndKeepLegacyPercentRounding()
    {
        List<GetSummaryQCInput> rows =
        [
            new() { CategoryName = " resin ", ExternalId = "A", QtyKg = 10, VoucherType = VoucherDetailType.QCPass },
            new() { CategoryName = "RESIN", ExternalId = "A", QtyKg = 20, VoucherType = VoucherDetailType.QCPass },
            new() { CategoryName = "RESIN", ExternalId = "B", QtyKg = 30, VoucherType = VoucherDetailType.QCFail },
            new() { CategoryName = "", ExternalId = "C", QtyKg = 40, VoucherType = VoucherDetailType.Special }
        ];
        var categories = IqcReportCalculator.BuildCategoryReport(rows);
        Assert.Equal(2, categories[0].ItemCount);
        Assert.Equal(60, categories[0].QtyKg);
        Assert.Equal(66.7m, categories[0].Percent);
        Assert.Equal("KHÁC", categories[1].CategoryName);
        var statuses = IqcReportCalculator.BuildQcStatusReport(rows);
        Assert.Equal(new[] { "HÀNG ĐẠT", "HÀNG LỖI", "CHỜ QC", "CHẤP NHẬN ĐẶC BIỆT" }, statuses.Select(x => x.StatusName));
        Assert.Equal(1, statuses[0].ItemCount);
        Assert.Equal(30, statuses[0].QtyKg);
        Assert.Equal(33.3m, statuses[0].Percent);
        Assert.Equal(0, statuses[2].Percent);
    }

    [Fact]
    public void Excel_PreservesTwoSheetsAndBothLegacyQuantityColumns()
    {
        var data = new QCInputByQCExcelExportData
        {
            DetailRows = [new() { MaterialExternalId = "MAT", ActualReceivedQuantityKg = 12.5m, DeliveryQuantityKg = 99 }],
            ReportRows = [new() { ExternalId = "MAT", QtyKg = 12.5m, VoucherType = VoucherDetailType.Special }]
        };
        using var workbook = new XLWorkbook(new MemoryStream(new IqcExcelRenderer().ExportExcel(data)));
        Assert.Equal(2, workbook.Worksheets.Count);
        var detail = workbook.Worksheet("QCInput");
        Assert.Equal("MAT", detail.Cell(4, 1).GetString());
        Assert.Equal(12.5, detail.Cell(4, 10).GetDouble());
        Assert.Equal(12.5, detail.Cell(4, 11).GetDouble());
        Assert.Equal("CHẤP NHẬN ĐẶC BIỆT", workbook.Worksheet("BaoCao").Cell(9, 6).GetString());
    }
}
