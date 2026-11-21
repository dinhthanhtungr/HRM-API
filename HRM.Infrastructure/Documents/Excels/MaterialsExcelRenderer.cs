using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;

namespace HRM.Infrastructure.Documents.Excels;

public sealed class MaterialsExcelRenderer : IMaterialsExcelRenderer
{
    public byte[] Render(IReadOnlyList<MaterialExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách NVL");
        string[] headers = ["Mã NVL", "Tên NVL", "Mã loại sản phẩm", "Tên loại sản phẩm"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).SetValue(headers[column]);
        sheet.Range(1, 1, 1, 4).Style.Font.Bold = true;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            sheet.Cell(index + 2, 1).SetValue(row.MaterialCode ?? string.Empty);
            var name = row.MaterialName ?? string.Empty;
            var displayName = string.IsNullOrWhiteSpace(row.VietausCode)
                ? name
                : string.IsNullOrWhiteSpace(name) ? $"({row.VietausCode.Trim()})" : $"{name} ({row.VietausCode.Trim()})";
            sheet.Cell(index + 2, 2).SetValue(displayName);
            sheet.Cell(index + 2, 3).SetValue(row.CategoryCode ?? string.Empty);
            sheet.Cell(index + 2, 4).SetValue(row.CategoryName ?? string.Empty);
        }
        sheet.Range(1, 1, rows.Count + 1, 4).SetAutoFilter();
        sheet.Column(1).Width = 26;
        sheet.Column(2).Width = 60;
        sheet.Column(3).Width = 24;
        sheet.Column(4).Width = 40;
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
