using HRM.Application.Features.Reports.PLPU;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Tests.Features.Reports.PLPU.Reference
{
    public class LegacyFinishExcelRenderer
    {
        public byte[] ExportFinishReportExcel(List<FinishRow> rows)
        {
            rows ??= new List<FinishRow>();

            rows = rows
                .OrderBy(x => x.ActualDeliveryDate)
                .ThenBy(x => x.CustomerName ?? string.Empty)
                .ThenBy(x => x.MerchandiseOrderCode ?? string.Empty)
                .ThenBy(x => x.ProductCode ?? string.Empty)
                .ToList();

            for (int i = 0; i < rows.Count; i++)
                rows[i].Stt = i + 1;

            var summary = BuildFinishSummary(rows);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Giao hàng hoàn tất");

            // ===== TITLE =====
            ws.Cell(1, 1).Value = "BÁO CÁO GIAO HÀNG HOÀN TẤT";
            ws.Range(1, 1, 1, 18).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // ===== DETAIL HEADER =====
            int headerRow = 3;

            string[] headers =
            {
                "STT",
                "Mã số",
                "Đơn hàng",
                "LSX",
                "Tên khách hàng",
                "Mã sản phẩm",
                "Tên sản phẩm",
                "Số lot",
                "Ngày nhận đơn",
                "Ngày yêu cầu giao",
                "Ngày dự kiến giao",
                "Ngày giao thực tế",
                "Ngày SX hoàn thành",
                "Trễ (ngày)",
                "SL đặt",
                "SL giao",
                "Địa chỉ",
                "Ghi chú"
            };

            for (int c = 1; c <= headers.Length; c++)
                ws.Cell(headerRow, c).Value = headers[c - 1];

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Alignment.WrapText = true;

            // ===== DETAIL DATA =====
            int startDataRow = headerRow + 1;

            for (int i = 0; i < rows.Count; i++)
            {
                int r = startDataRow + i;
                var item = rows[i];

                ws.Cell(r, 1).Value = item.Stt;
                ws.Cell(r, 2).Value = item.DeliveryOrderCode;
                ws.Cell(r, 3).Value = item.MerchandiseOrderCode;
                ws.Cell(r, 4).Value = item.MfgOrderCode;
                ws.Cell(r, 5).Value = item.CustomerName;
                ws.Cell(r, 6).Value = item.ProductCode;
                ws.Cell(r, 7).Value = item.ProductName;
                ws.Cell(r, 8).Value = item.BatchNo;

                if (item.OrderReceivedDate.HasValue)
                {
                    ws.Cell(r, 9).Value = item.OrderReceivedDate.Value;
                    ws.Cell(r, 9).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                if (item.DeliveryRequestDate.HasValue)
                {
                    ws.Cell(r, 10).Value = item.DeliveryRequestDate.Value;
                    ws.Cell(r, 10).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                if (item.ExpectedDeliveryDate.HasValue)
                {
                    ws.Cell(r, 11).Value = item.ExpectedDeliveryDate.Value;
                    ws.Cell(r, 11).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                if (item.ActualDeliveryDate.HasValue)
                {
                    ws.Cell(r, 12).Value = item.ActualDeliveryDate.Value;
                    ws.Cell(r, 12).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                if (item.ProductionCompletedDate.HasValue)
                {
                    ws.Cell(r, 13).Value = item.ProductionCompletedDate.Value;
                    ws.Cell(r, 13).Style.DateFormat.Format = "dd/MM/yyyy";
                }

                var lateDays = CalculateLateDays(item.ExpectedDeliveryDate, item.ActualDeliveryDate);
                ws.Cell(r, 14).Value = lateDays;
                ws.Cell(r, 15).Value = item.OrderedQuantity;
                ws.Cell(r, 16).Value = item.DeliveredQuantity;
                ws.Cell(r, 17).Value = item.Address;
                ws.Cell(r, 18).Value = item.Note;

                ws.Cell(r, 14).Style.NumberFormat.Format = "#,##0.##";
                ws.Cell(r, 15).Style.NumberFormat.Format = "#,##0.##";

                var rowRange = ws.Range(r, 1, r, 18);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(r, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(r, 7).Style.Alignment.WrapText = true;
                ws.Cell(r, 8).Style.Alignment.WrapText = true;
                ws.Cell(r, 17).Style.Alignment.WrapText = true;
                ws.Cell(r, 18).Style.Alignment.WrapText = true;

                if ((lateDays) > 0)
                {
                    ws.Cell(r, 14).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(r, 14).Style.Font.Bold = true;
                }
            }

            // ===== SUMMARY TABLE Ở PHÍA DƯỚI =====
            int sRow = startDataRow + rows.Count + 2;

            ws.Cell(sRow, 1).Value = "Nội dung";
            ws.Cell(sRow, 2).Value = "Tổng";
            ws.Cell(sRow, 3).Value = "Tỷ lệ %";
            ws.Range(sRow, 1, sRow, 3).Style.Fill.BackgroundColor = XLColor.BrightGreen;
            ws.Range(sRow, 1, sRow, 3).Style.Font.Bold = true;
            ws.Range(sRow, 1, sRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(sRow, 1, sRow, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(sRow, 1, sRow, 3).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var summaryRows = new[]
            {
                new { Label = "Tổng số đơn hàng", Total = summary.TotalOrders, Percent = (decimal?)null },
                new { Label = "Số đơn hàng đạt yêu cầu", Total = summary.OnTimeOrders, Percent = (decimal?)summary.OnTimePercent },
                new { Label = "Số đơn hàng giao không đạt yêu cầu", Total = summary.LateOrders, Percent = (decimal?)summary.LatePercent }
            };

            for (int i = 0; i < summaryRows.Length; i++)
            {
                int r = sRow + 1 + i;
                var item = summaryRows[i];

                ws.Cell(r, 1).Value = item.Label;
                ws.Cell(r, 2).Value = item.Total;
                ws.Cell(r, 3).Value = item.Percent;

                if (item.Percent.HasValue)
                    ws.Cell(r, 3).Style.NumberFormat.Format = "0\\%";

                var range = ws.Range(r, 1, r, 3);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                if (i >= 3)
                    range.Style.Fill.BackgroundColor = XLColor.Yellow;
            }

            int rateRow = sRow + 1 + summaryRows.Length;
            ws.Cell(rateRow, 1).Value = "Tỷ lệ % số đơn hàng đạt yêu cầu";
            ws.Cell(rateRow, 2).Value = summary.OnTimePercent;
            ws.Cell(rateRow, 3).Value = summary.LatePercent;
            ws.Cell(rateRow, 2).Style.NumberFormat.Format = "0\\%";
            ws.Cell(rateRow, 3).Style.NumberFormat.Format = "0\\%";

            var rateRange = ws.Range(rateRow, 1, rateRow, 3);
            rateRange.Style.Fill.BackgroundColor = XLColor.BrightGreen;
            rateRange.Style.Font.Bold = true;
            rateRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rateRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // ===== PHẦN NHẬN XÉT Ở DƯỚI SUMMARY =====
            int noteRow = rateRow + 2;

            ws.Cell(noteRow, 1).Value =
                $"Có tất cả {summary.TotalOrders} đơn hàng, trong đó số đơn hàng giao đúng hẹn là {summary.OnTimeOrders} đơn, chiếm {summary.OnTimePercent}% trên tổng số đơn hàng.";
            ws.Range(noteRow, 1, noteRow, 8).Merge();
            ws.Cell(noteRow, 1).Style.Alignment.WrapText = true;


            string conclusion = summary.OnTimePercent >= 80
                ? "đạt mục tiêu đề ra"
                : "không đạt mục tiêu đề ra";

            ws.Cell(noteRow + 7, 1).Value = $"Kết luận: {conclusion}";
            ws.Cell(noteRow + 7, 1).Style.Font.Bold = true;
            ws.Cell(noteRow + 7, 1).Style.Font.FontColor = XLColor.Red;

            // ===== WIDTH =====
            ws.Column(1).Width = 40;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 10;
            ws.Column(5).Width = 20;
            ws.Column(6).Width = 16;
            ws.Column(7).Width = 24;
            ws.Column(8).Width = 20;
            ws.Column(9).Width = 14;
            ws.Column(10).Width = 14;
            ws.Column(11).Width = 14;
            ws.Column(12).Width = 14;
            ws.Column(13).Width = 14;
            ws.Column(14).Width = 10;
            ws.Column(15).Width = 12;
            ws.Column(16).Width = 12;
            ws.Column(17).Width = 30;
            ws.Column(18).Width = 20;

            ws.SheetView.FreezeRows(headerRow);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            int finalRow = Math.Max(noteRow + 7, startDataRow + rows.Count - 1);
            if (finalRow >= 1)
            {
                ws.Rows(1, finalRow).AdjustToContents();
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ========================================================= Helpers =========================================================
        //private FinishReportSummary BuildFinishSummary(List<FinishRow> rows)
        //{
        //    rows ??= new List<FinishRow>();

        //    var orderGroups = rows
        //        .Where(x => !string.IsNullOrWhiteSpace(x.MerchandiseOrderCode))
        //        .GroupBy(x => x.MerchandiseOrderCode)
        //        .ToList();

        //    int total = orderGroups.Count;

        //    // Một đơn chỉ cần có 1 dòng giao trễ thì tính đơn đó là không đạt
        //    int late = orderGroups.Count(g => g.Any(IsLate));

        //    // Các đơn không trễ, bao gồm ExpectedDeliveryDate == null, sẽ mặc định đạt
        //    int onTime = total - late;

        //    decimal Percent(int value) =>
        //        total == 0 ? 0 : Math.Round(value * 100m / total, 0);

        //    return new FinishReportSummary
        //    {
        //        TotalOrders = total,
        //        OnTimeOrders = onTime,
        //        LateOrders = late,

        //        OnTimePercent = Percent(onTime),
        //        LatePercent = Percent(late),
        //    };
        //}

        private FinishReportSummary BuildFinishSummary(List<FinishRow> rows)
        {
            rows ??= new List<FinishRow>();

            var orderRows = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.MerchandiseOrderCode))
                .GroupBy(x => x.MerchandiseOrderCode)
                .Select(g =>
                {
                    var expectedDate = g
                        .Where(x => x.ExpectedDeliveryDate.HasValue)
                        .Select(x => x.ExpectedDeliveryDate!.Value.Date)
                        .DefaultIfEmpty()
                        .Min();

                    var firstActualDate = g
                        .Where(x => x.ActualDeliveryDate.HasValue)
                        .Select(x => x.ActualDeliveryDate!.Value.Date)
                        .DefaultIfEmpty()
                        .Min();

                    bool hasExpectedDate = g.Any(x => x.ExpectedDeliveryDate.HasValue);
                    bool hasActualDate = g.Any(x => x.ActualDeliveryDate.HasValue);

                    bool isLate =
                        hasExpectedDate &&
                        hasActualDate &&
                        firstActualDate > expectedDate;

                    return new
                    {
                        MerchandiseOrderCode = g.Key,
                        IsLate = isLate
                    };
                })
                .ToList();

            int total = orderRows.Count;
            int late = orderRows.Count(x => x.IsLate);
            int onTime = total - late;

            decimal Percent(int value) =>
                total == 0 ? 0 : Math.Round(value * 100m / total, 0);

            return new FinishReportSummary
            {
                TotalOrders = total,
                OnTimeOrders = onTime,
                LateOrders = late,
                OnTimePercent = Percent(onTime),
                LatePercent = Percent(late),
            };
        }

        private static bool IsLate(FinishRow row)
        {
            if (!row.ExpectedDeliveryDate.HasValue)
                return false; // Không có ngày hứa giao => mặc định đạt

            if (!row.ActualDeliveryDate.HasValue)
                return false; // Nếu report hoàn tất mà thiếu ngày thực giao => tạm mặc định đạt

            return row.ActualDeliveryDate.Value.Date > row.ExpectedDeliveryDate.Value.Date;
        }

        private static int CalculateLateDays(DateTime? expectedDeliveryDate, DateTime? actualDeliveryDate)
        {
            if (!expectedDeliveryDate.HasValue || !actualDeliveryDate.HasValue)
                return 0;

            return Math.Max(0, (actualDeliveryDate.Value.Date - expectedDeliveryDate.Value.Date).Days);
        }
    }
}
