using HRM.Application.Features.PLM.ColorChipRecords.Dtos;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public interface IColorChipRecordFiveOptionPdf
    {
        byte[] Render(ColorChipRecordPdfModel model, bool templateOnly = false);
        byte[] RenderTemplate();
    }
}
