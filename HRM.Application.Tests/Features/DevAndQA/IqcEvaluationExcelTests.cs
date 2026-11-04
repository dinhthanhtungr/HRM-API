using ClosedXML.Excel;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Infrastructure.Documents.Excels.IQC;
using HRM.Infrastructure.Documents.Pdfs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
namespace HRM.Application.Tests.Features.DevAndQA;
public sealed class IqcEvaluationExcelTests
{
    [Theory]
    [InlineData("SaleUser", 403)]
    [InlineData("QCUser", 404)]
    public async Task ExportRejectsUnauthorizedOrOutsideCompanyBeforeRendering(string role, int status)
    {
        var (db, store) = HRM.Application.Tests.Features.DevAndQA.IQC.IqcTestStore.CreateContext<HRM.Application.Abstractions.Persistence.DevAndQA.IEvaluationDbContext>();
        var user = new HRM.Application.Tests.Features.DevAndQA.IQC.IqcWorkflowTests.User { Roles = [role] };
        var permissions = new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user);
        store.Seed("WarehouseVoucherDetails", new HRM.Domain.Entities.WarehouseSchema.WarehouseVoucherDetail {
            VoucherDetailId = 7, ProductCode = "PRIVATE", Voucher = new HRM.Domain.Entities.WarehouseSchema.WarehouseVoucher { CompanyId = Guid.NewGuid() } });
        var services = new EvaluationServicesFixture(db, user, permissions, new());
        var access = new HRM.Application.Features.DevAndQA.Shared.Evaluations.Services.EvaluationAccessService(db, user, permissions);
        var reader = new HRM.Application.Features.DevAndQA.IQC.Services.Evaluations.IqcEvaluationHistoryReader(db, user, permissions);
        var handler = new HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcEvaluationExcel.ExportIqcEvaluationExcelQueryHandler(db, access, services.Iqc, reader, null!);
        var error = await Assert.ThrowsAsync<HRM.Application.Features.DevAndQA.Shared.Evaluations.EvaluationRequestException>(() => handler.Handle(new(7), default));
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void ExportMatchesByStandardAndPreservesSnapshotsAndText()
    {
        var root = FindRoot();
        var renderer = new IqcEvaluationExcelRenderer(Options.Create(new PdfOptions()), new TestHost { ContentRootPath = Path.Combine(root, "HRM.Api") });
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        EvaluationResultDto Row(Guid id, string name, string? english, string? value, int sort, string unit = "%") =>
            new(Guid.NewGuid(), id, null, name, name, name, true, value,
                new(EvaluationDataType.Decimal, unit, null, null, "ASTM", null, null, null, false, sort), english);
        IqcEvaluationDto Eval(params EvaluationResultDto[] rows) => new(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, rows);
        var current = Eval(Row(a, "Độ ẩm", "Moisture", "0", 1), Row(b, "Tỷ trọng", null, null, 2));
        var old = Eval(Row(b, "Tỷ trọng", "Density", "1.25", 1), Row(a, "Độ ẩm", "Moisture", "0.15", 2));
        var content = renderer.Render([new(new(2026, 10, 8), "MAT", "Material", "LOT1", current),
            new(new(2026, 10, 7), "MAT", "Material", null, old)]);
        using var workbook = new XLWorkbook(new MemoryStream(content));
        var sheet = workbook.Worksheet(1);
        Assert.Single(sheet.Pictures);
        Assert.Equal("Độ ẩm", sheet.Cell(3, 1).GetString());
        Assert.Equal("Moisture", sheet.Cell(3, 2).GetString());
        Assert.Equal("—", sheet.Cell(4, 2).GetString());
        Assert.Equal("%", sheet.Cell(3, 3).GetString());
        Assert.Equal("ASTM", sheet.Cell(3, 4).GetString());
        Assert.Equal(0, sheet.Cell(3, 5).GetDouble());
        Assert.Equal(0.15, sheet.Cell(3, 6).GetDouble());
        Assert.Equal("—", sheet.Cell(4, 5).GetString());
        Assert.Equal(1.25, sheet.Cell(4, 6).GetDouble());
        Assert.Equal("Chỉ tiêu (Tiếng Việt)", sheet.Cell(2, 1).GetString());
        Assert.Equal("Test item (English)", sheet.Cell(2, 2).GetString());
        Assert.Equal(XLColor.Yellow, sheet.Cell(1, 5).Style.Fill.BackgroundColor);
        Assert.Equal(XLBorderStyleValues.Thin, sheet.Cell(3, 5).Style.Border.BottomBorder);
        Assert.Contains("08.10.2026", sheet.Cell(1, 5).GetString());
        Assert.DoesNotContain(workbook.Worksheets.SelectMany(x => x.CellsUsed()), c => c.HasFormula);
    }
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "HRM.Api", "Assets"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Không tìm thấy repo assets.");
    }
    private sealed class TestHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
