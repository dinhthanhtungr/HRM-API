using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;

namespace HRM.Infrastructure.Documents.Excels;

public sealed class MerchandiseOrderVaExcelRenderer : IMerchandiseOrderVaExcelRenderer
{
    public byte[] Render(IReadOnlyList<MerchandiseOrderVaExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Merchandise Order VA");
        sheet.Cell(1, 1).SetValue("Merchandise Order ExternalId");
        sheet.Cell(1, 2).SetValue("ColourCode");
        sheet.Cell(1, 3).SetValue("VA ExternalId");
        sheet.Range(1, 1, 1, 3).Style.Font.Bold = true;
        for (var index = 0; index < rows.Count; index++)
        {
            sheet.Cell(index + 2, 1).SetValue(rows[index].MerchandiseOrderExternalId);
            sheet.Cell(index + 2, 2).SetValue(rows[index].ColourCode ?? string.Empty);
            sheet.Cell(index + 2, 3).SetValue(rows[index].VaExternalId ?? string.Empty);
        }
        sheet.Range(1, 1, rows.Count + 1, 3).SetAutoFilter();
        sheet.Column(1).Width = 36;
        sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 28;
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
