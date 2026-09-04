using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.Documents.Pdfs.ColorChipRecords;
using QuestPDF.Infrastructure;

namespace HRM.Application.Tests.Features.PLM.ColorChipRecords;

public sealed class ColorChipRecordPdfRendererTests
{
    public static TheoryData<FormStyle> SupportedStyles => new()
    {
        FormStyle.Chips2,
        FormStyle.Chips3,
        FormStyle.ChipsTanPhu,
        FormStyle.Chips2_NonStandard,
        FormStyle.ChipsTanPhuBacNinh,
        FormStyle.Chips5Options,
        FormStyle.ChipsTanPhuBacNinh3Thresholds
    };

    [Theory]
    [MemberData(nameof(SupportedStyles))]
    public void Render_SupportedLegacyStyle_ReturnsPdf(FormStyle formStyle)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var renderer = new ColorChipRecordPdfRenderer();
        var model = CreateModel(formStyle);

        var result = renderer.Render(model, formStyle);

        Assert.True(result.Length > 10_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result, 0, 4));
    }

    [Fact]
    public void Render_TanPhuThenBacNinh_ReturnsBothDocuments()
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var renderer = new ColorChipRecordPdfRenderer();

        var tanPhu = renderer.Render(CreateModel(FormStyle.ChipsTanPhu), FormStyle.ChipsTanPhu);
        var bacNinh = renderer.Render(
            CreateModel(FormStyle.ChipsTanPhuBacNinh),
            FormStyle.ChipsTanPhuBacNinh);

        Assert.True(tanPhu.Length > 10_000);
        Assert.True(bacNinh.Length > 10_000);
    }

    private static ColorChipRecordPdfModel CreateModel(FormStyle formStyle) => new()
    {
        BatchNo = "DF-001",
        Date = new DateTime(2026, 9, 4),
        Customer = "QA CUSTOMER",
        Code = "VA-001",
        Color = "SIGNAL RED",
        AddRate = "2.5%",
        Resin = "PP",
        PreparedBy = "QA USER",
        Machine = "Injection 01",
        TemperatureLimit = "190-210°C",
        SizeText = "2.4-3.3 ± 10%",
        PelletWeightGram = 60m,
        NetWeightGram = "100 g",
        Electrostatic = false,
        PrintNote = "PRINT NOTE",
        DeltaE = "≤ 1.0",
        RecordTypeText = RecordType.product.ToString(),
        ResinTypeText = ResinType.PP.ToString(),
        LogoTypeText = LogoType.Others.ToString(),
        FormStyleText = formStyle.ToString(),
        DevelopmentFormulaCodes = ["DF-001", "DF-002"]
    };
}
