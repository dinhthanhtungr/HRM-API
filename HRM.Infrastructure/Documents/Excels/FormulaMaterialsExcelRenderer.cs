using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.Formulas.Dtos.Exports;

namespace HRM.Infrastructure.Documents.Excels;

internal sealed class FormulaMaterialsExcelRenderer : IFormulaMaterialsExcelRenderer
{
    public byte[] Render(FormulaMaterialsExcelExportDocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Danh sách NVL");

        worksheet.Cell(1, 1).Value = $"Danh sách NVL công thức {document.FormulaExternalId}";
        worksheet.Range(1, 1, 1, 5).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        if (!string.IsNullOrWhiteSpace(document.FormulaName))
        {
            worksheet.Cell(2, 1).Value = document.FormulaName;
            worksheet.Range(2, 1, 2, 5).Merge();
        }

        const int headerRow = 4;
        var headers = new[] { "STT", "Mã NVL", "Tên NVL", "STD", "Giá gần nhất" };
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(headerRow, column + 1).Value = headers[column];
        }

        var headerRange = worksheet.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("D9EAF7");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (var index = 0; index < document.Materials.Count; index++)
        {
            var material = document.Materials[index];
            var row = headerRow + index + 1;
            worksheet.Cell(row, 1).Value = material.LineNo;
            worksheet.Cell(row, 2).Value = material.MaterialCode;
            worksheet.Cell(row, 3).Value = material.MaterialName;
            worksheet.Cell(row, 4).Value = material.Quantity;
            if (material.LatestUnitPrice.HasValue)
            {
                worksheet.Cell(row, 5).Value = material.LatestUnitPrice.Value;
            }
        }

        var lastRow = headerRow + Math.Max(document.Materials.Count, 1);
        var tableRange = worksheet.Range(headerRow, 1, lastRow, headers.Length);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        worksheet.Column(1).Width = 10;
        worksheet.Column(2).Width = 20;
        worksheet.Column(3).Width = 42;
        worksheet.Column(4).Width = 16;
        worksheet.Column(5).Width = 20;
        worksheet.Column(4).Style.NumberFormat.Format = "#,##0.######";
        worksheet.Column(5).Style.NumberFormat.Format = "#,##0.######";
        worksheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
