using HRM.Application.Features.PLM.ColorChipRecords.Dtos;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ColorChipRecordTanPhuBacNinhPdf : IColorChipRecordTanPhuBacNinhPdf
    {
        private readonly ColorChipRecordTanPhuPdf _renderer = new("1.8 * 3.4", "80-120");

        public byte[] RenderTemplate() => _renderer.RenderTemplate();

        public byte[] Render(ColorChipRecordPdfModel model, bool templateOnly = false) =>
            _renderer.Render(model, templateOnly);
    }
}
