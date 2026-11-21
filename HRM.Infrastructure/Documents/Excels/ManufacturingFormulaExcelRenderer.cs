using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.Manufacturing.Dtos;

namespace HRM.Infrastructure.Documents.Excels
{
    public class ManufacturingFormulaExcelRenderer : IManufacturingFormulaExcelRenderer
    {
        public byte[] Render(IReadOnlyList<ProductionOrderQuantityExcelRow> rows)
        {
            rows ??= Array.Empty<ProductionOrderQuantityExcelRow>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Formula");

            // Header
            ws.Cell(1, 1).Value = "TP";
            ws.Cell(1, 2).Value = "Công thức";
            ws.Cell(1, 3).Value = "ColorCode";
            ws.Cell(1, 4).Value = "Tên sản phẩm";
            ws.Cell(1, 5).Value = "Mã NVL";
            ws.Cell(1, 6).Value = "Detail";
            ws.Cell(1, 7).Value = "Số std";
            var includePrices = rows.Any(x => x.UnitPrice.HasValue);
            var columns = includePrices ? 8 : 7;
            if (includePrices) ws.Cell(1, 8).Value = "Đơn giá lưu trên VA (VND/kg)";

            var header = ws.Range(1, 1, 1, columns);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.Yellow;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Data
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = i + 2;

                ws.Cell(row, 1).SetValue(r.TP ?? "");
                ws.Cell(row, 2).SetValue(r.Formula ?? "");
                ws.Cell(row, 3).SetValue(r.ColorCode ?? "");
                ws.Cell(row, 4).SetValue(r.ProductName ?? "");
                ws.Cell(row, 5).SetValue(r.MaterialCode ?? "");
                ws.Cell(row, 6).SetValue(r.Detail ?? "");
                ws.Cell(row, 7).SetValue(r.StdQuantity);
                if (includePrices && r.UnitPrice.HasValue) ws.Cell(row, 8).SetValue(r.UnitPrice.Value);
            }

            // Border toàn bảng
            var lastRow = Math.Max(1, rows.Count + 1);
            var table = ws.Range(1, 1, lastRow, columns);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Wrap cho cột dài để “mở rộng xuống dòng” thay vì kéo ngang quá rộng
            ws.Column(4).Style.Alignment.WrapText = true; // Tên sản phẩm
            ws.Column(6).Style.Alignment.WrapText = true; // Detail

            // Canh giữa vài cột giống bảng
            ws.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Format số std
            ws.Column(7).Style.NumberFormat.Format = "0.0000000";
            if (includePrices) { ws.Column(8).Style.NumberFormat.Format = "#,##0.##"; ws.Column(8).Width = 30; }

            // AutoFit: chỉ auto-fit các cột ngắn
            ws.Columns(1, 5).AdjustToContents(); // TP..Mã NVL
            ws.Column(7).AdjustToContents();     // Số std

            // Cột dài: set width hợp lý để nhìn đẹp + không “phình” file
            ws.Column(4).Width = 28; // Tên sp
            ws.Column(6).Width = 40; // Detail

            // AutoFit chiều cao row theo wrap text (để chữ không bị che)
            ws.Rows(1, lastRow).AdjustToContents();

            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
