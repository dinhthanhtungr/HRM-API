using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
namespace HRM.Infrastructure.Documents.Excels;
internal sealed partial class DeliveryReportExcelRenderer : IDeliveryReportExcelRenderer
{
        public byte[] ExportDeliveryFinish(List<DeliveryFinishRow> rows, DateTime fromDate, DateTime toDate)
        {
            rows ??= new List<DeliveryFinishRow>();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("DeliveryFinish");

            // ===== Title =====
            ws.Cell(1, 1).Value = "BÁO CÁO GIAO HÀNG HOÀN TẤT";
            ws.Range(1, 1, 1, 15).Merge();
            ws.Range(1, 1, 1, 15).Style.Font.Bold = true;
            ws.Range(1, 1, 1, 15).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Từ ngày: {fromDate:dd/MM/yyyy}  -  Đến ngày: {toDate:dd/MM/yyyy}";
            ws.Range(2, 1, 2, 15).Merge();
            ws.Range(2, 1, 2, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ===== Header =====
            var headerRow = 4;
            string[] headers =
            {
                "Mã số", "Đơn hàng", "Ngày nhận đơn hàng", "Ngày yêu cầu giao hàng",
                "Ngày hứa giao hàng", "Ngày thực tế giao hàng", "Khách hàng", "Người giao",
                "Sản phẩm", "Kho", "Batch #", "Số lượng (kg)", "Số bao", "Số PO", "Ghi chú"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(headerRow, c + 1).Value = headers[c];
            }

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 230, 230);
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // ===== Body =====
            var r = headerRow + 1;
            foreach (var x in rows)
            {
                ws.Cell(r, 1).Value = x.DeliveryExternalId;
                ws.Cell(r, 2).Value = x.OrderExternalId;

                ws.Cell(r, 3).Value = x.OrderCreatedDate;
                ws.Cell(r, 4).Value = x.DeliveryRequestDate;
                ws.Cell(r, 5).Value = x.ExpectedDeliveryDate;
                ws.Cell(r, 6).Value = x.DeliveryActualDate;

                ws.Cell(r, 7).Value = x.CustomerName;
                ws.Cell(r, 8).Value = x.DelivererName;
                ws.Cell(r, 9).Value = x.ProductDisplay;

                ws.Cell(r, 10).Value = x.WarehouseDisplay;
                ws.Cell(r, 11).Value = x.LotNoOrBatch;

                ws.Cell(r, 12).Value = x.QuantityKg;
                ws.Cell(r, 13).Value = x.NumOfBags;

                ws.Cell(r, 14).Value = x.PoNo;
                ws.Cell(r, 15).Value = x.Note;

                r++;
            }

            var lastDataRow = r - 1;

            // ===== Format =====
            ws.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(4).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(5).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(6).Style.DateFormat.Format = "dd/MM/yyyy";

            ws.Column(12).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(13).Style.NumberFormat.Format = "#,##0";

            if (lastDataRow >= headerRow + 1)
            {
                var dataRange = ws.Range(headerRow + 1, 1, lastDataRow, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                dataRange.Style.Alignment.WrapText = true;
            }

            ws.SheetView.FreezeRows(headerRow);
            ws.Columns().AdjustToContents();

            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.3;
            ws.PageSetup.Margins.Right = 0.3;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

}
