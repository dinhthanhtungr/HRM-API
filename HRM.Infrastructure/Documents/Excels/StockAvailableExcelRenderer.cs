using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Infrastructure.Documents.Excels;

public sealed class StockAvailableExcelRenderer : IStockAvailableExcelRenderer
{
    public byte[] Render(IReadOnlyList<StockAvailableDto> items, StockAvailableExportVisibility visibility, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.AddWorksheet("Tong hop");
        var details = workbook.AddWorksheet("Chi tiet ke lot");
        string[] summaryHeaders = ["Mã hàng", "Tên hàng", "Nhóm hàng", "Loại tồn", "Tổng tồn kệ hoạt động kg", "Tồn kệ đã tắt kg", "Tổng tồn vật lý kg", "Đang giữ kg", "Khả dụng kg"];
        string[] detailHeaders = ["Mã hàng", "Tên hàng", "Loại tồn", "Lot", "Kệ", "Trạng thái kệ", "Công ty", "Tồn kg", "Lý do ghi nhận nhập lỗi", "Phiếu nguồn", "Yêu cầu nguồn", "Phạm vi lý do"];
        Header(summary, summaryHeaders);
        Header(details, detailHeaders);
        var row = 2;
        var detailRow = 2;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            summary.Cell(row, 1).Value = item.Code;
            summary.Cell(row, 2).Value = item.CodeName;
            summary.Cell(row, 3).Value = item.CategoryName;
            summary.Cell(row, 4).Value = StockTypeName(item.StockType);
            summary.Cell(row, 5).Value = item.TotalOnHandKg;
            summary.Cell(row, 6).Value = item.InactiveShelfOnHandKg;
            summary.Cell(row, 7).Value = item.PhysicalOnHandKg;
            summary.Cell(row, 8).Value = item.ReservedOpenAllKg;
            summary.Cell(row, 9).Value = item.AvailableKg;
            row++;
            foreach (var detail in item.StockDetailAvailables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                details.Cell(detailRow, 1).Value = item.Code;
                details.Cell(detailRow, 2).Value = item.CodeName;
                details.Cell(detailRow, 3).Value = StockTypeName(item.StockType);
                details.Cell(detailRow, 4).Value = detail.LotNo ?? string.Empty;
                details.Cell(detailRow, 5).Value = detail.ShelfStockCode;
                details.Cell(detailRow, 6).Value = detail.ShelfIsActive ? "Hoạt động" : "Đã tắt";
                details.Cell(detailRow, 7).Value = detail.CompanyName;
                details.Cell(detailRow, 8).Value = detail.OnHandKg;
                details.Cell(detailRow, 9).Value = string.Join("\n", detail.DefectReasons.Select(x => x.Reason).Distinct());
                details.Cell(detailRow, 10).Value = string.Join("\n", detail.DefectReasons.Select(x => x.VoucherCode).Distinct());
                details.Cell(detailRow, 11).Value = string.Join("\n", detail.DefectReasons.Select(x => x.RequestCode).Distinct());
                details.Cell(detailRow, 12).Value = detail.DefectReasons.Count > 0
                    ? "Theo phiếu nhập cùng mã/lot; lý do xác nhận nhập, không phải kết luận QC hoặc phân bổ phần tồn còn lại."
                    : item.StockType == StockType.DefectiveFinishedGood ? "Chưa ghi nhận lý do liên kết" : string.Empty;
                detailRow++;
            }
        }
        Finish(summary, row - 1, summaryHeaders.Length);
        Finish(details, detailRow - 1, detailHeaders.Length);
        if (row > 2) summary.Range(2, 5, row - 1, 9).Style.NumberFormat.Format = "#,##0.###;[Red]-#,##0.###";
        if (detailRow > 2) details.Range(2, 8, detailRow - 1, 8).Style.NumberFormat.Format = "#,##0.###;[Red]-#,##0.###";
        // Omit inaccessible columns from the actual workbook, never merely hide them.
        // Physical stock is not a displayed column on the stock-available screen.
        summary.Column(7).Delete();
        if (!visibility.InactiveShelfFields)
        {
            summary.Column(6).Delete();
            details.Column(6).Delete();
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Header(IXLWorksheet sheet, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EFFD");
        header.Style.Font.FontColor = XLColor.FromHtml("#17202E");
        sheet.SheetView.FreezeRows(1);
    }

    private static void Finish(IXLWorksheet sheet, int lastRow, int columns)
    {
        sheet.Range(1, 1, Math.Max(lastRow, 1), columns).SetAutoFilter();
        sheet.Columns(1, columns).Width = 22;
        sheet.Column(2).Width = 40;
        sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.RangeUsed()!.Style.Alignment.WrapText = true;
    }

    private static string StockTypeName(StockType type) => type switch
    {
        StockType.FinishedGood => "TP Đạt", StockType.DefectiveFinishedGood => "TP Lỗi",
        StockType.RawMaterial => "NVL Đạt", StockType.DefectiveRawMaterial => "NVL Lỗi",
        StockType.Waiter => "Chờ QC", StockType.Material => "Vật tư", _ => "Khác"
    };
}
