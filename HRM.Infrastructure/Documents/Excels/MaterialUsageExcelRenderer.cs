using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;
namespace HRM.Infrastructure.Documents.Excels;
public sealed class MaterialUsageExcelRenderer : IMaterialUsageExcelRenderer
{
    public byte[] Render(IReadOnlyList<MaterialUsageExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("VA theo NVL");
        string[] headers = ["STT", "Mã màu", "Tên sản phẩm", "VA gần nhất", "NVL sử dụng", "Ngày VA mới nhất"];
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).SetValue(headers[c]);
        var header = sheet.Range(1, 1, 1, 6);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightBlue;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            sheet.Cell(i + 2, 1).SetValue(i + 1);
            sheet.Cell(i + 2, 2).SetValue(row.ColourCode);
            sheet.Cell(i + 2, 3).SetValue(row.ProductName);
            sheet.Cell(i + 2, 4).SetValue(row.VaCode);
            sheet.Cell(i + 2, 5).SetValue(row.MaterialCodes);
            sheet.Cell(i + 2, 6).SetValue(row.LatestVaDate);
        }
        double[] widths = [8, 22, 60, 24, 60, 23];
        for (var c = 0; c < widths.Length; c++) sheet.Column(c + 1).Width = widths[c];
        sheet.Column(6).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Columns(3, 5).Style.Alignment.WrapText = true;
        sheet.Range(1, 1, rows.Count + 1, 6).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}