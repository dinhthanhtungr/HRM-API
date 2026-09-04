using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords;

internal sealed class ColorChipRecordPdfRenderer : IColorChipRecordPdfRenderer
{
    private readonly IColorChipRecordPortraitPdf _portrait = new ColorChipRecordPortraitPdf();
    private readonly IColorChipRecordLandscapePdf _landscape = new ColorChipRecordLandscapePdf();
    private readonly IColorChipRecordTanPhuPdf _tanPhu = new ColorChipRecordTanPhuPdf();
    private readonly IColorChipRecordTanPhuBacNinhPdf _tanPhuBacNinh = new ColorChipRecordTanPhuBacNinhPdf();
    private readonly IColorChipRecordFiveOptionPdf _fiveOption = new ColorChipRecordFiveOptionPdf();
    private readonly IColorChipRecordTanPhuBacNinh3ThresholdPdf _threeThreshold = new ColorChipRecordTanPhuBacNinh3ThresholdPdf();

    public byte[] Render(ColorChipRecordPdfModel model, FormStyle formStyle)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (formStyle == FormStyle.ChipsTanPhu)
        {
            model.StandardText = string.Empty;
        }

        return formStyle switch
        {
            FormStyle.Chips2 => _portrait.Render(model),
            FormStyle.Chips3 => _landscape.Render(model),
            FormStyle.ChipsTanPhu => _tanPhu.Render(model),
            FormStyle.ChipsTanPhuBacNinh => _tanPhuBacNinh.Render(model),
            FormStyle.Chips5Options => _fiveOption.Render(model),
            FormStyle.ChipsTanPhuBacNinh3Thresholds => _threeThreshold.Render(model),
            FormStyle.Chips2_NonStandard => _portrait.Render(model),
            _ => _landscape.Render(model)
        };
    }
}
