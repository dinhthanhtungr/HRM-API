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

        sheet.Range(5, 1, 5, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        sheet.Columns(4, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns(7, 8).Style.NumberFormat.Format = "#,##0";
        sheet.Column(9).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.Columns().AdjustToContents(8, 45);
        sheet.SheetView.FreezeRows(5);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
