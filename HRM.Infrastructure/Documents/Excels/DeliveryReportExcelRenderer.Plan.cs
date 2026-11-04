using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
namespace HRM.Infrastructure.Documents.Excels;
internal sealed partial class DeliveryReportExcelRenderer : IDeliveryReportExcelRenderer
{
        public byte[] ExportDeliveryPlanExcel(List<DeliveryPlanRow> rows)
        {
            rows ??= new List<DeliveryPlanRow>();

            // ====== SORT để các dòng cùng nhóm đứng sát nhau (merge mới hoạt động) ======
            rows = rows
                .OrderBy(x => x.CustomerName ?? "")
                .ThenBy(x => x.Address ?? "")
                .ThenBy(x => x.Factory ?? "")
                .ThenBy(x => x.PickupTimeText ?? "")
                .ThenBy(x => x.ExpectedDeliveryDate)
                .ThenBy(x => x.Driver ?? "")
                .ThenBy(x => x.Code ?? "")
                .ThenBy(x => x.LotNo ?? "")
                .ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Stt = i + 1;
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("DS giao hàng");

            // ====== NOTE ======
            ws.Cell(1, 1).Value = "Ghi chú: dấu (*) thể hiện rằng sản phẩm này chưa sản xuất xong nên chưa có tổng kết số lượng.";
            ws.Range(1, 1, 1, 11).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;

            // ====== HEADER ======
            var headerRow = 3;
            string[] headers =
            {
                "STT",
                "Tên khách hàng",
                "Code",
                "Số Lot",
                "Số Lượng",
                "Nhà Máy",
                "Thời gian dự kiến lấy hàng",
                "Ngày dự kiến giao",
                "Tài xế",
                "Ghi Chú",
                "Địa chỉ"
            };

            for (int c = 1; c <= headers.Length; c++)
                ws.Cell(headerRow, c).Value = headers[c - 1];

            var headerRange = ws.Range(headerRow, 1, headerRow, 11);
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Alignment.WrapText = true;
            ws.Row(headerRow).Height = 22;

            // ====== DATA ======
            var startDataRow = headerRow + 1;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = startDataRow + i;
                var item = rows[i];

                ws.Cell(r, 1).Value = item.Stt;
                ws.Cell(r, 2).Value = item.CustomerName;
                ws.Cell(r, 3).Value = item.Code;
                ws.Cell(r, 4).Value = item.LotNo;
                ws.Cell(r, 5).Value = item.QuantityText;
                ws.Cell(r, 6).Value = item.Factory;
                ws.Cell(r, 7).Value = item.PickupTimeText;

                if (item.ExpectedDeliveryDate.HasValue)
                {
                    ws.Cell(r, 8).Value = item.ExpectedDeliveryDate.Value;
                    ws.Cell(r, 8).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                ws.Cell(r, 9).Value = item.Driver;
                ws.Cell(r, 10).Value = item.Note;
                ws.Cell(r, 11).Value = item.Address;

                // style dòng
                var rowRange = ws.Range(r, 1, r, 11);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // canh giữa
                ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Quantity đỏ nếu pending
                if (item.QuantityIsPending)
                {
                    ws.Cell(r, 5).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(r, 5).Style.Font.Bold = true;
                }

                // wrap cho cột dài
                ws.Cell(r, 4).Style.Alignment.WrapText = true;
                ws.Cell(r, 11).Style.Alignment.WrapText = true;
            }

            // ====== MERGE theo nhóm giao hàng (liên tiếp) ======
            // merge cột: Tên KH (2), Nhà máy (6), Thời gian lấy hàng (7), Ngày dự kiến giao (8), Tài xế (9), Địa chỉ (11)
            MergeByGroup(ws, rows, startDataRow,
                colsToMerge: new[] { 2, 6, 7, 8, 9, 11 },
                keySelector: x => $"{x.CustomerName}");

            // ====== WIDTH ======
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 24;
            ws.Column(4).Style.Alignment.WrapText = true;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 10;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 14;
            ws.Column(9).Width = 14;
            ws.Column(10).Width = 14;
            ws.Column(11).Width = 40;

            ws.SheetView.FreezeRows(headerRow);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            // nếu address wrap -> tăng chiều cao row cho đẹp
            if (rows.Count > 0)
            {
                var lastRow = startDataRow + rows.Count - 1;
                ws.Rows(startDataRow, lastRow).AdjustToContents();
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void MergeByGroup(
            IXLWorksheet ws,
            List<DeliveryPlanRow> rows,
            int startDataRow,
            int[] colsToMerge,
            Func<DeliveryPlanRow, string> keySelector)
        {
            if (rows.Count <= 1) return;

            string KeyAt(int idx)
                => (keySelector(rows[idx]) ?? "").Trim().ToLowerInvariant();

            var firstRow = startDataRow;
            var lastRow = startDataRow + rows.Count - 1;

            int groupStartRow = firstRow;

            for (int excelRow = firstRow + 1; excelRow <= lastRow + 1; excelRow++)
            {
                bool isEnd = (excelRow == lastRow + 1);

                int prevIdx = (excelRow - 1) - firstRow;
                int currIdx = excelRow - firstRow;

                var prevKey = KeyAt(prevIdx);
                var currKey = isEnd ? "__end__" : KeyAt(currIdx);

                if (isEnd || prevKey != currKey)
                {
                    int groupEndRow = excelRow - 1;

                    if (groupEndRow > groupStartRow)
                    {
                        foreach (var col in colsToMerge)
                        {
                            var rng = ws.Range(groupStartRow, col, groupEndRow, col);
                            rng.Merge();
                            rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                            // nếu merge address thì wrap vẫn giữ
                            if (col == 10)
                                rng.Style.Alignment.WrapText = true;
                        }
                    }

                    groupStartRow = excelRow;
                }
            }
        }


}
