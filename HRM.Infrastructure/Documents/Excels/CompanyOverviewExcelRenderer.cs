using ClosedXML.Excel;
using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;

namespace HRM.Infrastructure.Documents.Excels;

internal sealed class CompanyOverviewExcelRenderer : ICompanyOverviewExcelRenderer
{
    private const int HeaderRow = 10;
    private const string NumberFormat = "#,##0.00;[Red]-#,##0.00;0.00";

    public byte[] Render(CompanyOverviewExportData data)
    {
        using var workbook = new XLWorkbook();
        Sales(workbook, data);
        RawMaterials(workbook, data);
        Supplies(workbook, data);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static IXLWorksheet Start(XLWorkbook workbook, string name, CompanyOverviewDto report,
        string basis, string[] columns)
    {
        var sheet = workbook.AddWorksheet(name);
        sheet.Cell(1, 1).Value = name.ToUpperInvariant();
        sheet.Range(1, 1, 1, columns.Length).Merge().Style.Font.SetBold().Font.SetFontSize(18);
        Row(sheet, 2, "Công ty", report.CompanyId.ToString());
        Row(sheet, 3, "Từ ngày", report.From, "Đến ngày", report.To);
        Row(sheet, 4, "Thời điểm đọc báo cáo", report.UpdatedAt);
        sheet.Cell(4, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        sheet.Cell(5, 1).Value = basis;
        sheet.Range(5, 1, 5, columns.Length).Merge().Style.Alignment.SetWrapText();
        sheet.Row(5).Height = 35;
        Row(sheet, 8, "Dữ liệu được đọc lại khi xuất; cùng quy tắc và kỳ báo cáo, không phải snapshot lúc mở trang.");
        sheet.Range(8, 1, 8, columns.Length).Merge().Style.Alignment.SetWrapText();
        Row(sheet, HeaderRow, columns.Cast<object?>().ToArray());
        sheet.Range(HeaderRow, 1, HeaderRow, columns.Length).Style.Font.SetBold()
            .Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2857A4"));
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 11;
        sheet.SheetView.FreezeRows(HeaderRow);
        return sheet;
    }

    private static void Sales(XLWorkbook workbook, CompanyOverviewExportData data)
    {
        var report = data.Report;
        var sheet = Start(workbook, "Doanh số", report,
            "Doanh số theo DeliveryRevenueQuery và phạm vi Sales hiện hành; ngày tạo phiếu giao trong kỳ, quy đổi VND. Không xuất giá vốn/lợi nhuận.",
            ["Ngày ghi nhận", "Mã DHG", "Khách hàng", "Mã sản phẩm", "Tên sản phẩm", "Đã giao (tấn)", "Doanh số (VND)"]);
        Row(sheet, 6, "Doanh số (VND)", report.Sales.Revenue, "Đã giao (tấn)", report.Sales.DeliveredQuantity / 1000m,
            "Đơn có giao", report.Sales.DeliveredOrderCount);
        var row = HeaderRow + 1;
        foreach (var item in data.Sales)
            Row(sheet, row++, item.Date, item.OrderCode, item.Customer,
                item.ProductCode, item.ProductName, item.QuantityKg / 1000m, item.AmountVnd);
        Finish(sheet, row, 7);
        Row(sheet, row, "Tổng chi tiết", null, null, null, null,
            data.Sales.Sum(x => x.QuantityKg) / 1000m, data.Sales.Sum(x => x.AmountVnd));
        Row(sheet, row + 1, "Chênh lệch chi tiết − báo cáo", null, null, null, null,
            data.Sales.Sum(x => x.QuantityKg) / 1000m - report.Sales.DeliveredQuantity / 1000m,
            data.Sales.Sum(x => x.AmountVnd) - report.Sales.Revenue);
    }

    private static void RawMaterials(XLWorkbook workbook, CompanyOverviewExportData data)
    {
        var report = data.Report;
        var sheet = Start(workbook, "NVL", report,
            "PLPU: tập PO tạo trong kỳ; lượng nhập/QC và tiền đã nhập theo phân bổ hiện hành, không lọc lại theo ngày nhập. Tiền VND trước VAT.",
            ["Mã PO", "Dòng", "Mã NVL", "Tên NVL", "Quy cách", "SL đặt (PLPU)", "SL nhập (PLPU)",
                "Đơn giá PO", "Tiền đặt (VND)", "Tiền nhập (VND)", "SL chờ QC", "SL loại", "Trạng thái"]);
        Row(sheet, 6, "Đặt mua (VND)", report.RawMaterials.TotalPurchaseValue,
            "Đã nhập (VND)", report.RawMaterials.ReceivedPurchaseValue, "Số PO", report.RawMaterials.TotalPO);
        var row = HeaderRow + 1;
        foreach (var item in data.RawMaterials)
            Row(sheet, row++, item.POExternalId, item.LineNo, item.MaterialCode, item.MaterialName, item.Package,
                item.OrderedQuantity, item.WarehouseReceivedQuantity, item.UnitPriceAgreed,
                Ordered(item), item.ReceivedPurchaseValue, item.PendingQcQuantity, item.RejectedQuantity, item.LineReportStatus);
        Finish(sheet, row, 13);
        Row(sheet, row, "Tổng tiền chi tiết", null, null, null, null, null, null, null,
            data.RawMaterials.Sum(Ordered), data.RawMaterials.Sum(x => x.ReceivedPurchaseValue));
        Row(sheet, row + 1, "Chênh lệch chi tiết − báo cáo", null, null, null, null, null, null, null,
            data.RawMaterials.Sum(Ordered) - report.RawMaterials.TotalPurchaseValue,
            data.RawMaterials.Sum(x => x.ReceivedPurchaseValue) - report.RawMaterials.ReceivedPurchaseValue);
    }

    private static decimal Ordered(HRM.Application.Features.Reports.PLPU.PLPUPurchaseOrderDetailReportDto row)
        => row.TotalPriceAgreed ?? row.OrderedQuantity * (row.UnitPriceAgreed ?? 0m);

    private static void Supplies(XLWorkbook workbook, CompanyOverviewExportData data)
    {
        var report = data.Report;
        var summary = report.Purchases.Groups.FirstOrDefault(x => x.Kind == "Supplies");
        var sheet = Start(workbook, "Vật tư", report,
            "Tiền đơn mua chưa VAT: purchase.v_po_ap_invoice_item_reconcile.po_amount_excl_vat; lọc ngày tạo PO, không yêu cầu hóa đơn. Tiền thiếu/không duy nhất để trống, không coi là 0.",
            ["Ngày tạo PO", "Mã PO", "Mã vật tư", "SL theo view (đơn vị nguồn)", "Tiền đơn mua (VND)", "Cần đối soát"]);
        Row(sheet, 6, "Tiền đơn đã xác định (VND)", summary?.KnownOrderedAmount ?? 0m,
            "Số đơn", summary?.OrderCount ?? 0, "Dòng thiếu tiền", summary?.OrderedAmountIssueCount ?? 0);
        var row = HeaderRow + 1;
        foreach (var item in data.Supplies)
            Row(sheet, row++, item.Date, item.DocumentCode, item.ItemCode,
                item.Issue is "AmbiguousPurchaseOrder" or "AmbiguousPurchaseAmount" ? null : item.Quantity,
                item.Amount, IssueLabel(item.Issue));
        Finish(sheet, row, 6);
        Row(sheet, row, "Tổng tiền xác định được", null, null, null, data.Supplies.Sum(x => x.Amount ?? 0m));
        Row(sheet, row + 1, "Chênh lệch chi tiết − báo cáo", null, null, null,
            data.Supplies.Sum(x => x.Amount ?? 0m) - (summary?.KnownOrderedAmount ?? 0m));
    }

    private static string IssueLabel(string? issue) => issue switch
    {
        null => "",
        "AmbiguousPurchaseOrder" => "Mã đơn mua không duy nhất",
        "AmbiguousPurchaseAmount" => "Tiền/số lượng đơn mua không duy nhất",
        "MissingOrderedAmount" => "Thiếu tiền đơn mua",
        _ => issue
    };

    private static void Row(IXLWorksheet sheet, int row, params object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            var cell = sheet.Cell(row, column + 1);
            switch (values[column])
            {
                case null: break;
                case DateTime date: cell.Value = date; cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                case decimal number: cell.Value = number; cell.Style.NumberFormat.Format = NumberFormat; break;
                case int count: cell.Value = count; break;
                // Text is always a literal cell value, never FormulaA1 (including strings starting with =/+/-/@).
                default: cell.Value = values[column]!.ToString(); break;
            }
        }
    }

    private static void Finish(IXLWorksheet sheet, int nextRow, int columns)
    {
        sheet.Range(HeaderRow, 1, Math.Max(HeaderRow, nextRow - 1), columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(HeaderRow, Math.Min(nextRow, HeaderRow + 200), 12, 42);
        sheet.Range(nextRow, 1, nextRow + 1, columns).Style.Font.SetBold();
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
    }
}
