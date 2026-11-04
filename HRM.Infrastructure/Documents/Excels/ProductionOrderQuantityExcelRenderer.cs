using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.Manufacturing.Dtos;

namespace HRM.Infrastructure.Documents.Excels
{
    public sealed class ProductionOrderQuantityExcelRenderer : IProductionOrderQuantityExcelRenderer
    {
        public byte[] Render(IReadOnlyList<ProductionOrderQuantityExcelRow> rows)
        {
            rows ??= Array.Empty<ProductionOrderQuantityExcelRow>();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Formula Quantity");

            worksheet.Cell(1, 1).Value = "TP";
            worksheet.Cell(1, 2).Value = "Công thức";
            worksheet.Cell(1, 3).Value = "ColorCode";
            worksheet.Cell(1, 4).Value = "Tên sản phẩm";
            worksheet.Cell(1, 5).Value = "Mã NVL";
            worksheet.Cell(1, 6).Value = "Detail";
            worksheet.Cell(1, 7).Value = "Số std";
            worksheet.Cell(1, 8).Value = "Khối lượng";

            var header = worksheet.Range(1, 1, 1, 8);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.Yellow;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            for (var index = 0; index < rows.Count; index++)
            {
                var item = rows[index];
                var row = index + 2;

                worksheet.Cell(row, 1).SetValue(item.TP);
                worksheet.Cell(row, 2).SetValue(item.Formula);
                worksheet.Cell(row, 3).SetValue(item.ColorCode);
                worksheet.Cell(row, 4).SetValue(item.ProductName);
                worksheet.Cell(row, 5).SetValue(item.MaterialCode);
                worksheet.Cell(row, 6).SetValue(item.Detail);
                worksheet.Cell(row, 7).SetValue(item.StdQuantity);
                worksheet.Cell(row, 8).SetValue(item.RequiredQuantity);
            }

            var lastRow = Math.Max(1, rows.Count + 1);
            var table = worksheet.Range(1, 1, lastRow, 8);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            worksheet.Column(4).Style.Alignment.WrapText = true;
            worksheet.Column(6).Style.Alignment.WrapText = true;

            foreach (var column in new[] { 1, 2, 3, 5, 7, 8 })
                worksheet.Column(column).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Column(7).Style.NumberFormat.Format = "0.0000000";
            worksheet.Column(8).Style.NumberFormat.Format = "0.#######";

            worksheet.Columns(1, 3).AdjustToContents();
            worksheet.Column(5).AdjustToContents();
            worksheet.Columns(7, 8).AdjustToContents();
            worksheet.Column(4).Width = 28;
            worksheet.Column(6).Width = 40;
            worksheet.Rows(1, lastRow).AdjustToContents();
            worksheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
