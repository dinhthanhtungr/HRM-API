using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

namespace HRM.Infrastructure.Documents.Excels;

internal sealed class ProductColourNamePreviewExcelRenderer : IProductColourNamePreviewExcelRenderer
{
    public byte[] Render(FillProductColourNamesResult preview)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tên màu dự kiến");
        string[] headers = ["ProductId", "Mã màu", "Tên sản phẩm", "Tên màu hiện tại", "Tên màu đề xuất", "Kết quả"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        var row = 2;
        foreach (var item in preview.Items)
        {
            sheet.Cell(row, 1).Value = item.ProductId.ToString();
            sheet.Cell(row, 2).Value = item.ColourCode ?? "";
            sheet.Cell(row, 3).Value = item.Name ?? "";
            sheet.Cell(row, 4).Value = "NULL";
            sheet.Cell(row, 5).Value = item.ColourName ?? "";
            sheet.Cell(row, 6).Value = item.ColourName is null ? "Bỏ qua: không rõ hoặc có nhiều màu" : "Sẽ cập nhật";
            row++;
        }
        sheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).SetAutoFilter();
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, 2).Width = 28;
        sheet.Column(3).Width = 65;
        sheet.Columns(4, 5).Width = 24;
        sheet.Column(6).Width = 42;
        sheet.Column(3).Style.Alignment.WrapText = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
