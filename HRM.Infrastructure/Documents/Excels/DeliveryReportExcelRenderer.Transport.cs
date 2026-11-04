using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
namespace HRM.Infrastructure.Documents.Excels;
internal sealed partial class DeliveryReportExcelRenderer : IDeliveryReportExcelRenderer
{
        public byte[] ExportTransportWorkbook(
            DeliveryTransportWorkbookData data,
            DateTime fromDate,
            DateTime toDate)
        {
            data ??= new DeliveryTransportWorkbookData();

            using var wb = new XLWorkbook();

            BuildSummarySheet(wb, data.SummaryRows, fromDate, toDate, data.CompanyName);

            foreach (var kv in data.DetailByDeliverer.OrderBy(x => x.Key))
            {
                BuildDelivererSheet(wb, kv.Key, kv.Value, fromDate);
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private void BuildSummarySheet(
            XLWorkbook wb,
            List<DeliveryTransportSummaryRow> rows,
            DateTime fromDate,
            DateTime toDate, string companyName)
        {
            var ws = wb.Worksheets.Add("Tổng kết");

            ws.Cell(1, 1).Value = $"ĐƠN VỊ: {companyName}";
            ws.Range(1, 1, 1, 8).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;

            ws.Cell(2, 1).Value = $"TỪ NGÀY {fromDate:dd/MM/yyyy} ĐẾN NGÀY {toDate:dd/MM/yyyy}";
            ws.Range(2, 1, 2, 8).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            ws.Cell(4, 2).Value = $"BÁO CÁO XÁC NHẬN VẬN CHUYỂN";
            ws.Range(4, 2, 4, 7).Merge();
            ws.Range(4, 2, 4, 7).Style.Font.Bold = true;
            ws.Range(4, 2, 4, 7).Style.Font.FontSize = 14;
            ws.Range(4, 2, 4, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var headerRow = 6;
            string[] headers =
            {
                "STT",
                "Tên tài xế",
                "Số chuyến",
                "Tổng số lượng",
                "Số tiền",
                "Trừ tiền ứng",
                "Còn nhận",
                "Ghi chú"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(headerRow, i + 1).Value = headers[i];
            }

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var currentRow = headerRow + 1;
            foreach (var item in rows)
            {
                ws.Cell(currentRow, 1).Value = item.Stt;
                ws.Cell(currentRow, 2).Value = item.DelivererName;
                ws.Cell(currentRow, 3).Value = item.TotalTrips;
                ws.Cell(currentRow, 4).Value = item.TotalQuantity;
                ws.Cell(currentRow, 5).Value = item.TotalAmount;
                ws.Cell(currentRow, 6).Value = item.AdvanceAmount;
                ws.Cell(currentRow, 7).Value = item.RemainingAmount;
                ws.Cell(currentRow, 8).Value = item.Note;

                currentRow++;
            }

            if (rows.Any())
            {
                var totalRow = currentRow;
                ws.Cell(totalRow, 1).Value = "Tổng cộng";
                ws.Range(totalRow, 1, totalRow, 2).Merge();

                ws.Cell(totalRow, 3).FormulaA1 = $"SUM(C{headerRow + 1}:C{totalRow - 1})";
                ws.Cell(totalRow, 4).FormulaA1 = $"SUM(D{headerRow + 1}:D{totalRow - 1})";
                ws.Cell(totalRow, 5).FormulaA1 = $"SUM(E{headerRow + 1}:E{totalRow - 1})";
                ws.Cell(totalRow, 6).FormulaA1 = $"SUM(F{headerRow + 1}:F{totalRow - 1})";
                ws.Cell(totalRow, 7).FormulaA1 = $"SUM(G{headerRow + 1}:G{totalRow - 1})";

                var totalRange = ws.Range(totalRow, 1, totalRow, 8);
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
                totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Column(1).Width = 8;
            ws.Column(2).Width = 24;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 15;
            ws.Column(5).Width = 16;
            ws.Column(6).Width = 16;
            ws.Column(7).Width = 16;
            ws.Column(8).Width = 30;

            ws.Column(4).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(5).Style.NumberFormat.Format = "#,##0";
            ws.Column(6).Style.NumberFormat.Format = "#,##0";
            ws.Column(7).Style.NumberFormat.Format = "#,##0";

            ws.SheetView.FreezeRows(headerRow);
        }

        private void BuildDelivererSheet(
            XLWorkbook wb,
            string delivererName,
            List<DeliveryTransportReportRow> rows,
            DateTime fromDate)
        {
            var sheetName = SafeSheetName(delivererName);
            var baseName = sheetName;
            for (var suffix = 2; wb.Worksheets.Contains(sheetName); suffix++)
            {
                var tail = $" ({suffix})";
                sheetName = baseName[..Math.Min(baseName.Length, 31 - tail.Length)] + tail;
            }
            var ws = wb.Worksheets.Add(sheetName);

            ws.Cell(1, 4).Value = "BẢNG THEO DÕI VẬN CHUYỂN";
            ws.Range(1, 4, 1, 8).Merge();
            ws.Range(1, 4, 1, 8).Style.Font.Bold = true;
            ws.Range(1, 4, 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 4).Value = $"Tháng {fromDate:MM/yyyy}";
            ws.Range(2, 4, 2, 8).Merge();
            ws.Range(2, 4, 2, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 4).Value = $"Người vận chuyển: {delivererName}";
            ws.Range(3, 4, 3, 8).Merge();
            ws.Range(3, 4, 3, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(3, 4, 3, 8).Style.Font.Bold = true;

            var headerRow = 5;
            string[] headers =
            {
                "STT",
                "Ngày giao",
                "Mã giao hàng",
                "Khách hàng",
                "Địa chỉ",
                "Sản phẩm",
                "Số lượng",
                "Số bao",
                "Số PO",
                "Lot No",
                "Thành tiền",
                "Ghi chú"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(headerRow, i + 1).Value = headers[i];
            }

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var row = headerRow + 1;
            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];

                ws.Cell(row, 1).Value = i + 1;
                ws.Cell(row, 2).Value = item.DeliveryDate;
                ws.Cell(row, 3).Value = item.DeliveryExternalId;
                ws.Cell(row, 4).Value = item.CustomerName;
                ws.Cell(row, 5).Value = item.Address;
                ws.Cell(row, 6).Value = item.ProductDisplay;
                ws.Cell(row, 7).Value = item.TotalQuantity;
                ws.Cell(row, 8).Value = item.TotalBags;
                ws.Cell(row, 9).Value = item.PoNoDisplay;
                ws.Cell(row, 10).Value = item.LotNoDisplay;
                ws.Cell(row, 11).Value = item.DeliveryPrice;
                ws.Cell(row, 12).Value = item.Note;

                var lineRange = ws.Range(row, 1, row, headers.Length);
                lineRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                lineRange.Style.Alignment.WrapText = true;

                row++;
            }

            if (rows.Any())
            {
                var totalRow = row;
                ws.Cell(totalRow, 1).Value = "Tổng cộng";
                ws.Range(totalRow, 1, totalRow, 6).Merge();

                ws.Cell(totalRow, 7).FormulaA1 = $"SUM(G{headerRow + 1}:G{totalRow - 1})";
                ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{headerRow + 1}:H{totalRow - 1})";
                ws.Cell(totalRow, 11).FormulaA1 = $"SUM(K{headerRow + 1}:K{totalRow - 1})";

                var totalRange = ws.Range(totalRow, 1, totalRow, headers.Length);
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
                totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Column(1).Width = 8;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 28;
            ws.Column(5).Width = 40;
            ws.Column(6).Width = 40;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 12;
            ws.Column(9).Width = 18;
            ws.Column(10).Width = 18;
            ws.Column(11).Width = 16;
            ws.Column(12).Width = 24;

            ws.Column(2).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(7).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(8).Style.NumberFormat.Format = "#,##0";
            ws.Column(11).Style.NumberFormat.Format = "#,##0";

            ws.SheetView.FreezeRows(headerRow);
        }

        private static string SafeSheetName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "ChuaPhanNguoiGiao";

            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            var cleaned = new string(input
                .Where(c => !invalidChars.Contains(c))
                .ToArray());

            if (cleaned.Length > 31)
                cleaned = cleaned.Substring(0, 31);

            return string.IsNullOrWhiteSpace(cleaned) ? "Sheet" : cleaned;
        }
}
