using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Infrastructure.Documents.Pdfs;
using HRM.Domain.Enums.Manufacturings;
using QuestPDF.Infrastructure;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingAdjustmentPdfTests
{
    [Theory]
    [InlineData(AdjustmentType.AddQuantity)]
    [InlineData(AdjustmentType.SetQuantity)]
    public void LegacyTemplate_RendersBothAdjustmentModesAndGroupedRanges(AdjustmentType mode)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var font = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OpenSans-Regular.ttf");
        using var stream = File.OpenRead(font);
        QuestPDF.Drawing.FontManager.RegisterFont(stream);
        var model = new PdfMfgFormulaAdjustment { ProductionOrderCode = "MFG-TEST", ProductCode = "PRODUCT", ProductName = "Sản phẩm",
            FormulaCode = "VA-TEST", CreatedDate = new(2026, 10, 10), CreatedByName = "QC Test", Note = "Kiểm tra bản in" };
        for (var no = 1; no <= 2; no++)
            model.Batches.Add(new() { BatchNo = no, TrialNo = 1, AdjustmentType = mode, FinalQuantityGram = 50500,
                Items = [new() { CategoryId = Guid.Parse("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b"), Code = "PIGMENT", Name = "Bột màu",
                    BaseQuantity = 50000, AdjustedQuantity = 50500, DeltaQuantity = 500, Unit = "g", LotNo = "LOT", LineNo = 1 }] });
        var bytes = new ManufacturingAdjustmentPdfRenderer().Render(model);
        Assert.True(bytes.Length > 10000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
