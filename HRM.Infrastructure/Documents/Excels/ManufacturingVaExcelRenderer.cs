using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

namespace HRM.Infrastructure.Documents.Excels;

public sealed class ManufacturingVaExcelRenderer : IManufacturingVaExcelRenderer
{
    private static readonly string[] DetailHeaders =
    [
        "Ngày tạo VA", "Ngày hoàn thành nhập kho TP", "DHG - Đơn đặt hàng", "Số lệnh (*) VA", "Mã khách hàng",
        "Trường hợp (TH)", "Loại hình (TM/SX/GC)", "Đầu ra của VA", "Tình trạng", "Diễn giải",
        "Mã thành phẩm (*)", "Mã hàng", "Tên thành phẩm", "ĐVT thành phẩm", "Số lượng TP (kế hoạch)",
        "Số lượng thực nhập", "Hợp đồng bán", "Mã đối tượng THCP (Thành phẩm)", "Mã thống kê (Thành phẩm)",
        "Mã nguyên vật liệu (*)", "Tên nguyên vật liệu", "ĐVT NVL", "Số lượng NVL/1 đơn vị SP", "Số lượng NVL",
        "Mã đối tượng THCP (Định mức)", "Mã khoản mục CP", "Mã thống kê (Định mức)", "Mã công trình"
    ];
    private static readonly string[] SummaryHeaders =
    [
        "VA", "Ngày tạo VA", "Ngày nhập TP đạt cuối", "Đơn hàng", "Mã KH từ đơn hàng", "Lệnh MFG",
        "Trường hợp", "Loại hình", "Đầu ra VA", "Status MFG", "Mã TP (SampleRequest)", "Mã hàng (ColourCode)",
        "Tên TP", "ĐVT", "Kế hoạch (kg)", "Thực nhập TP đạt (kg)", "Số dòng công thức", "Đúng 1 NVL",
        "Công đoạn", "Nhóm TP", "Cần kiểm tra"
    ];

    public byte[] Render(IReadOnlyList<ManufacturingVaExcelRow> rows, CancellationToken ct = default)
    {
        // Excel limits include the header; never silently truncate a large export.
        if (rows.Sum(x => (long)Math.Max(1, x.Items.Count)) > XLHelper.MaxRowNumber - 1)
            throw new InvalidOperationException("Báo cáo vượt giới hạn dòng Excel; hãy thu hẹp khoảng ngày tạo VA.");
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("VA tổng hợp");
        var detail = workbook.Worksheets.Add("Chi tiết theo mẫu");
        var trade = workbook.Worksheets.Add("Hàng thương mại");
        var notes = workbook.Worksheets.Add("Quy tắc");
        WriteHeader(summary, SummaryHeaders);
        WriteHeader(detail, DetailHeaders);
        WriteHeader(trade, SummaryHeaders);
        var detailIndex = 2;
        var tradeIndex = 2;
        for (var index = 0; index < rows.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[index];
            WriteSummary(summary, index + 2, row);
            if (row.IsSingleMaterial) WriteSummary(trade, tradeIndex++, row);
            if (row.Items.Count == 0) WriteDetail(detail, detailIndex++, row, null);
            else foreach (var item in row.Items) WriteDetail(detail, detailIndex++, row, item);
        }
        Format(summary, rows.Count + 1, SummaryHeaders.Length, [2, 3], [15, 16]);
        Format(detail, detailIndex - 1, DetailHeaders.Length, [1, 2], [15, 16, 23, 24]);
        Format(trade, tradeIndex - 1, SummaryHeaders.Length, [2, 3], [15, 16]);
        WriteRules(notes);
        ct.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(IXLWorksheet sheet, int index, ManufacturingVaExcelRow r)
    {
        object?[] cells = [r.VaCode, r.CreatedDate, r.LastReceiptDate, r.OrderCodes, r.CustomerCodes, r.MfgCode,
            r.Case, r.ProductionType, r.Output, r.Status, r.ProductExternalId, r.ColourCode, r.ProductName,
            "Kg", r.PlannedQuantity, r.ReceivedQuantity, r.Items.Count, r.IsSingleMaterial ? "Có" : "Không",
            r.Operation, r.CategoryName, r.Notes];
        WriteCells(sheet, index, cells);
    }

    private static void WriteDetail(IXLWorksheet sheet, int index, ManufacturingVaExcelRow r, ManufacturingVaExcelItem? item)
    {
        object?[] cells = [r.CreatedDate, r.LastReceiptDate, r.OrderCodes, r.VaCode, r.CustomerCodes, r.Case,
            r.ProductionType, r.Output, r.Status, r.Notes, r.ProductExternalId, r.ColourCode, r.ProductName,
            "Kg", r.PlannedQuantity, r.ReceivedQuantity, null, r.ProductExternalId, null,
            item?.Code, item?.Name, "Kg", item?.StandardQuantity, item?.StandardQuantity * r.PlannedQuantity,
            r.ProductExternalId, null, null, null];
        WriteCells(sheet, index, cells);
    }

    private static void WriteCells(IXLWorksheet sheet, int index, object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            if (values[column] is null) continue;
            var cell = sheet.Cell(index, column + 1);
            switch (values[column])
            {
                case null: break;
                case DateTime date: cell.SetValue(date); break;
                case decimal number: cell.SetValue(number); break;
                case int count: cell.SetValue(count); break;
                case string text: cell.SetValue(text); break;
            }
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, string[] headers)
    {
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).SetValue(headers[column]);
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#243B53");
        header.Style.Alignment.WrapText = true;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 45;
    }

    private static void Format(IXLWorksheet sheet, int lastRow, int columns, int[] dates, int[] numbers)
    {
        sheet.Columns(1, columns).Width = 24;
        foreach (var column in dates) sheet.Column(column).Style.DateFormat.Format = "dd/MM/yyyy";
        foreach (var column in numbers) sheet.Column(column).Style.NumberFormat.Format = "#,##0.##########";
        sheet.Range(1, 1, lastRow, columns).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.ShowGridLines = false;
        var isDetail = columns == DetailHeaders.Length;
        sheet.Column(13).Width = 55;
        sheet.Column(isDetail ? 10 : 21).Width = 90;
        sheet.Column(isDetail ? 6 : 7).Width = 48;
        if (isDetail) sheet.Column(21).Width = 55;
    }

    private static void WriteRules(IXLWorksheet sheet)
    {
        WriteHeader(sheet, ["Nội dung", "Quy tắc"]);
        string[][] rules =
        [
            ["Phạm vi", "VA active trong công ty đăng nhập; lọc theo ngày tạo ManufacturingFormula, không phải ngày tạo đơn hàng."],
            ["Ngày nhập và thực nhập", "Phiếu nhập TP đạt (loại 1) APPLIED, dòng IsApplied/IsIncrease, QtyKg > 0; khớp company và lot VA. Ngày nhập cuối không chứng minh đã hoàn thành kế hoạch."],
            ["Không có phiếu", "Ngày và lượng nhập để trống, không ghi 0. Không cộng TP lỗi, tồn đầu, trả hàng hay chuyển kho."],
            ["TM", "Đúng 1 dòng NVL đang hoạt động (Material/MaterialFailure, có MaterialId, không ProductId) → TH1/TM, ưu tiên hơn công đoạn. Một Product/ProductFailure không tính TM."],
            ["Các TH khác", "Nghiền→TH6/SX; Hoàn thiện→TH4/SX; Băm gia công→GC ngoài 6 TH; nhóm GCO→GC; Bột màu/phụ gia→TH2/SX; Trộn/Recolor→TH5/SX; Đùn→TH3/SX. Thiếu công đoạn có thể đề xuất theo nhóm TP, chưa đủ căn cứ để trống loại hình."],
            ["Đơn hàng và KH", "MfgOrderPOs → dòng đơn → đơn active cùng company. KH=Customer.ExternalId của đơn, không fallback KH trên MFG/SampleRequest. Nhiều đơn/khách được ghép bằng dấu chấm phẩy."],
            ["Đầu ra", "Internal hoặc KH_VIETAUS→Nội bộ; SampleRequest→Đơn mẫu cho khách; Merchandise→Đơn bán cho khách; Complaint→Đơn khiếu nại cần xác nhận. Thiếu đơn để trống."],
            ["Thành phẩm", "Mã TP=SampleRequest.ExternalId active cùng company theo Product; mã hàng=Product.ColourCode; tên=Product.Name; trạng thái=MFG.Status."],
            ["Kế hoạch và định mức", "Kế hoạch=MFG.TotalQuantity (khối lượng sản xuất của lệnh), không lấy TotalQuantityRequest. Chưa có thì để trống, không fallback. STD=ManufacturingFormulaMaterial.Quantity; lượng NVL=STD × kế hoạch. ĐVT đặt Kg theo yêu cầu; chưa quy đổi nguồn khác kg và có cảnh báo."],
            ["Chi tiết NVL", "Mã/tên snapshot trong ManufacturingFormulaMaterial; thiếu snapshot mới fallback quan hệ nguồn cùng company. Không xuất giá/cost."],
            ["Liên kết MFG", "Ưu tiên lựa chọn hiện hành, khử trùng theo MFG ID; nếu không có thì dùng liên kết lịch sử duy nhất và ghi chú. Nhiều MFG khác nhau để trống dữ liệu MFG thay vì chọn ngẫu nhiên."],
            ["Cộng số lượng", "Chỉ cộng kế hoạch/thực nhập tại VA tổng hợp: mỗi VA một dòng. Sheet chi tiết lặp số lượng TP theo từng NVL theo mẫu."],
            ["Cột chưa có nguồn", "Hợp đồng bán, mã thống kê, khoản mục CP, công trình để trống. Mã đối tượng THCP dùng mã TP theo ví dụ mẫu, cần duyệt trước khi import AMIS."]
        ];
        for (var index = 0; index < rules.Length; index++) WriteCells(sheet, index + 2, rules[index]);
        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 115;
        sheet.Range(2, 1, rules.Length + 1, 2).Style.Alignment.WrapText = true;
        sheet.Rows(2, rules.Length + 1).Height = 60;
        sheet.ShowGridLines = false;
    }
}
