using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

namespace HRM.Infrastructure.Documents.Excels;

internal sealed class DeliveryOrderExcelRenderer : IDeliveryOrderExcelRenderer
{
    public byte[] Render(DeliveryOrderDocumentDto model)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Phieu giao hang");
        sheet.Cell(1, 1).Value = "PHIẾU GIAO HÀNG";
        sheet.Range(1, 1, 1, 8).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        Header(sheet, 2, "Đơn vị", model.CompanyName);
        Header(sheet, 3, "Số phiếu", model.ExternalId ?? model.Id.ToString());
        Header(sheet, 4, "Trạng thái", model.Status);
        sheet.Cell(4, 5).Value = "Ngày tạo";
        if (model.CreatedDate.HasValue) sheet.Cell(4, 6).Value = model.CreatedDate.Value;
        sheet.Cell(4, 6).Style.DateFormat.Format = "dd/MM/yyyy";
        Header(sheet, 5, "Khách hàng", model.CustomerName);
        Header(sheet, 6, "Địa chỉ giao", model.DeliveryAddress);
        Header(sheet, 7, "Người nhận / ĐT", $"{model.Receiver} / {model.Phone}");
        Header(sheet, 8, "MST / Thanh toán", $"{model.TaxNumber} / {model.PaymentType}");
        Header(sheet, 9, "Người giao", string.Join(", ", model.Deliverers));
        Header(sheet, 10, "Ghi chú", model.Note);
        string[] headers = ["STT", "Mã SP", "Tên sản phẩm", "Lot / Batch", "Số lượng (kg)", "Số bao", "PO", "Hàng kèm"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(12, i + 1).Value = headers[i];
        var row = 13;
        foreach (var line in model.Lines)
        {
            sheet.Cell(row, 1).Value = row - 12;
            // Gán Value dưới dạng text, không gán FormulaA1 với dữ liệu từ người dùng.
            sheet.Cell(row, 2).Value = line.ProductCode ?? string.Empty;
            sheet.Cell(row, 3).Value = line.ProductName ?? string.Empty;
            sheet.Cell(row, 4).Value = line.LotNo ?? string.Empty;
            sheet.Cell(row, 5).Value = line.Quantity;
            sheet.Cell(row, 6).Value = line.NumOfBags;
            sheet.Cell(row, 7).Value = line.PONo ?? string.Empty;
            sheet.Cell(row, 8).Value = line.IsAttach ? "Có" : "Không";
            row++;
        }
        sheet.Cell(row, 3).Value = "Tổng (không gồm hàng kèm)";
        sheet.Cell(row, 5).Value = model.TotalQuantity;
        sheet.Cell(row, 6).Value = model.TotalBags;
        sheet.Range(row, 1, row, 8).Style.Font.Bold = true;
        sheet.Cell(row + 2, 1).Value = "Ngày tạo không phải ngày xác nhận giao. Xuất file không cập nhật trạng thái phiếu.";
        sheet.Range(row + 2, 1, row + 2, 8).Merge();
        sheet.Range(12, 1, 12, 8).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Range(12, 1, 12, 8).Style.Font.Bold = true;
        if (row > 13) sheet.Range(12, 1, row - 1, 8).SetAutoFilter();
        sheet.Column(1).Width = 7; sheet.Column(2).Width = 19; sheet.Column(3).Width = 35;
        sheet.Column(4).Width = 25; sheet.Column(5).Width = 18; sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 22; sheet.Column(8).Width = 12;
        sheet.Column(5).Style.NumberFormat.Format = "#,##0.###";
        sheet.Column(6).Style.NumberFormat.Format = "0";
        sheet.Cell(4, 6).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.RangeUsed()!.Style.Alignment.WrapText = true;
        sheet.Rows(1, row + 2).AdjustToContents();
        // Excel không tự tăng chiều cao merged cell; dành chỗ cho địa chỉ/ghi chú dài.
        for (var headerRow = 2; headerRow <= 10; headerRow++)
        {
            var value = sheet.Cell(headerRow, 3).GetString();
            var visualLines = value.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 90d)));
            sheet.Row(headerRow).Height = Math.Max(22, visualLines * 18);
        }
        sheet.Row(1).Height = 30;
        sheet.Row(12).Height = 32;
        sheet.SheetView.FreezeRows(12);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(12, 12);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Header(IXLWorksheet sheet, int row, string label, string? value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Range(row, 1, row, 2).Merge().Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = value ?? string.Empty;
        sheet.Range(row, 3, row, row == 4 ? 4 : 8).Merge();
    }
}
