using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

namespace HRM.Infrastructure.Documents.Excels;

internal sealed class PurchaseOrderExcelRenderer : IPurchaseOrderExcelRenderer
{
    public byte[] Render(PurchaseOrderDetailDto purchaseOrder)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Purchase Order");
        sheet.Cell("A1").Value = "PURCHASE ORDER";
        sheet.Range("A1:K1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell("A2").Value = "Mã PO"; sheet.Cell("B2").Value = purchaseOrder.ExternalId;
        sheet.Cell("D2").Value = "Nhà cung cấp"; sheet.Cell("E2").Value = purchaseOrder.SupplierName;
        sheet.Cell("A3").Value = "Trạng thái"; sheet.Cell("B3").Value = purchaseOrder.Status;
        sheet.Cell("D3").Value = "Đơn bán liên quan"; sheet.Cell("E3").Value = purchaseOrder.MerchandiseOrderCodes;

        string[] headers = ["STT", "Mã vật tư", "Tên vật tư", "SL đặt", "SL đã nhập", "Quy cách", "Đơn giá", "Thành tiền", "Ngày giao", "Ghi chú"];
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(5, index + 1).Value = headers[index];

        var row = 6;
        foreach (var line in purchaseOrder.Items)
        {
            sheet.Cell(row, 1).Value = line.LineNo;
            sheet.Cell(row, 2).Value = line.MaterialCode;
            sheet.Cell(row, 3).Value = line.MaterialName;
            sheet.Cell(row, 4).Value = line.Quantity;
            sheet.Cell(row, 5).Value = line.RealQuantity ?? 0;
            sheet.Cell(row, 6).Value = line.Package;
            sheet.Cell(row, 7).Value = line.UnitPriceAgreed;
            sheet.Cell(row, 8).Value = line.TotalPriceAgreed;
            if (line.DeliveryDate.HasValue) sheet.Cell(row, 9).Value = line.DeliveryDate.Value;
            sheet.Cell(row, 10).Value = line.Note;
            row++;
        }

        sheet.Cell(row, 7).Value = "Tổng trước VAT"; sheet.Cell(row++, 8).Value = purchaseOrder.Subtotal;
        sheet.Cell(row, 7).Value = $"VAT ({purchaseOrder.Vat ?? 0}%)"; sheet.Cell(row++, 8).Value = purchaseOrder.VatAmount;
        sheet.Cell(row, 7).Value = "Tổng có VAT"; sheet.Cell(row, 8).Value = purchaseOrder.TotalPrice;
        sheet.Range(5, 1, 5, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        sheet.Columns(4, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns(7, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(9).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Columns().AdjustToContents(8, 45);
        sheet.SheetView.FreezeRows(5);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
    public byte[] RenderList(IReadOnlyList<PurchaseOrderExcelRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Purchase Orders");

        var headers = new[]
        {
                "Ngày đặt hàng",
                "Số đơn hàng",
                "Trạng thái",
                "Phân loại",
                "Ngày giao hàng",
                "Ngày Y/c giao hàng",
                "Số ngày giao muộn",
                "Đạt/Không đạt",
                "Mã NCC",
                "Tên NCC",
                "Mã hàng",
                "Tên hàng",
                "ĐVT",
                "Số lượng đặt",
                "Số lượng nhập kho",
                "Số lượng QC đạt",
                "Số lượng chờ QC",
                "Số lượng QC fail",
                "Đơn giá",
                "Thành tiền thực nhận"
            };

        for (var c = 1; c <= headers.Length; c++)
            ws.Cell(1, c).Value = headers[c - 1];

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.Yellow;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.WrapText = true;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        for (var i = 0; i < rows.Count; i++)
        {
            var r = i + 2;
            var item = rows[i];

            SetDateCell(ws.Cell(r, 1), item.OrderDate);
            ws.Cell(r, 2).Value = item.PurchaseOrderCode;
            ws.Cell(r, 3).Value = item.Status;
            ws.Cell(r, 4).Value = item.OrderType;
            SetDateCell(ws.Cell(r, 5), item.ActualDeliveryDate);
            SetDateCell(ws.Cell(r, 6), item.RequestDeliveryDate);
            ws.Cell(r, 7).Value = item.LateDays;
            ws.Cell(r, 8).Value = item.DeliveryResult;
            ws.Cell(r, 9).Value = item.SupplierExternalId;
            ws.Cell(r, 10).Value = item.SupplierName;
            ws.Cell(r, 11).Value = item.MaterialCode;
            ws.Cell(r, 12).Value = item.MaterialName;
            ws.Cell(r, 13).Value = item.Unit;
            ws.Cell(r, 14).Value = item.OrderedQuantity;
            ws.Cell(r, 15).Value = item.ReceivedQuantity;
            ws.Cell(r, 16).Value = item.AcceptedQuantity;
            ws.Cell(r, 17).Value = item.PendingQcQuantity;
            ws.Cell(r, 18).Value = item.RejectedQuantity;
            ws.Cell(r, 19).Value = item.UnitPrice;
            ws.Cell(r, 20).Value = item.RealTotalAmount;
        }

        var lastRow = Math.Max(1, rows.Count + 1);
        var table = ws.Range(1, 1, lastRow, headers.Length);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Columns(14, 20).Style.NumberFormat.Format = "#,##0.##";
        ws.Columns(1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Columns(9, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Column(10).Style.Alignment.WrapText = true;
        ws.Column(12).Style.Alignment.WrapText = true;

        ws.Columns(1, 11).AdjustToContents();
        ws.Columns(13, 20).AdjustToContents();
        ws.Column(12).Width = 36;
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetDateCell(IXLCell cell, DateTime? value)
    {
        if (!value.HasValue)
            return;

        cell.Value = value.Value;
        cell.Style.DateFormat.Format = "dd/MM/yyyy";
    }

}
