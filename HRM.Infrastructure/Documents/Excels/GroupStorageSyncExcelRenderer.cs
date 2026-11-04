using ClosedXML.Excel;
using HRM.Application.Features.InternalMail.GroupStorage;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Infrastructure.Documents.Excels;

// GROUP-STORAGE-TRANSITION: synchronization-api — per-run file, employee IDs only; no bodies, names or credentials.
public sealed class GroupStorageSyncExcelRenderer : IGroupStorageSyncExcelRenderer
{
    public byte[] Render(GroupStorageSyncReport report)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Theo mã màu");
        var detail = workbook.Worksheets.Add("Chi tiết nhóm");
        var membership = workbook.Worksheets.Add("Bù President");
        foreach (var sheet in workbook.Worksheets)
        {
            sheet.ShowGridLines = false;
            sheet.Style.Font.FontName = "Arial";
            sheet.Style.Font.FontSize = 11;
            sheet.Cell(1, 1).Value = "ĐỐI CHIẾU ĐỒNG BỘ NHÓM";
            sheet.Cell(1, 1).Style.Font.FontSize = 16;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Value = $"Lần chạy: {report.RunId:D} | Công ty: {report.CompanyId:D}";
            sheet.Cell(3, 1).Value = $"UTC: {report.StartedAt:yyyy-MM-dd HH:mm:ss} | Kết quả: {report.Outcome}";
            sheet.Cell(4, 1).Value = report.Issues.Count == 0 ? (report.IncludePresidentMemberships
                ? "Mapping + bù President Kỹ thuật/Giá. Không đổi tin, read-state, mute, archive hoặc outbox; không tự thêm Custom."
                : "Chỉ mapping; không bù thành viên. Giữ nguyên tin nhắn, thành viên và trạng thái đọc.") :
                (report.Outcome == "Unknown" ? "CHƯA XÁC NHẬN COMMIT: " : "KHÔNG ÁP DỤNG: ") + string.Join("; ", report.Issues);
            sheet.Cell(4, 1).Style.Font.FontColor = report.Issues.Count == 0 ? XLColor.DarkSlateGray : XLColor.DarkRed;
            sheet.SheetView.FreezeRows(6);
        }
        string[] head = ["Mã hồ sơ", "Mã màu", "Nhóm hiện có", "Mới đồng bộ", "Đã có trước", "Tin Chung", "Tin Kỹ thuật", "Tin Giá", "Tin nhóm riêng", "Tin chưa xóa", "Tin đã xóa", "ID hồ sơ gốc", "President bổ sung", "President đã có", "President bỏ qua"];
        for (var i = 0; i < head.Length; i++) summary.Cell(6, i + 1).Value = head[i];
        var row = 7;
        foreach (var group in report.Rows.GroupBy(r => r.RootConversationId))
        {
            var first = group.First();
            summary.Cell(row, 1).Value = first.RecordCode;
            if (!string.IsNullOrEmpty(first.ColourCode)) summary.Cell(row, 2).Value = first.ColourCode;
            summary.Cell(row, 3).Value = group.Count();
            summary.Cell(row, 4).Value = group.Count(r => r.Result == "Mới đồng bộ");
            summary.Cell(row, 5).Value = group.Count(r => r.Result == "Đã có trước");
            foreach (var kind in Enum.GetValues<InternalConversationGroupType>())
                summary.Cell(row, 6 + (int)kind).Value = group.Where(r => r.GroupType == kind).Sum(r => r.VisibleMessages);
            summary.Cell(row, 10).Value = group.Sum(r => r.VisibleMessages);
            summary.Cell(row, 11).Value = group.Sum(r => r.DeletedMessages);
            summary.Cell(row, 12).Value = first.RootConversationId.ToString("D");
            var members = report.PresidentMemberships.Where(m => m.RootConversationId == first.RootConversationId).ToArray();
            summary.Cell(row, 13).Value = members.Count(m => m.Status == PresidentMembershipSyncStatus.Added);
            summary.Cell(row, 14).Value = members.Count(m => m.Status == PresidentMembershipSyncStatus.Existing);
            summary.Cell(row, 15).Value = members.Count(m => m.Status is not (PresidentMembershipSyncStatus.Added or PresidentMembershipSyncStatus.Existing));
            row++;
        }
        string[] detailHead = ["Mã hồ sơ", "Mã màu", "Tên nhóm", "Đang dùng", "Kết quả lần này", "Tin chưa xóa", "Tin đã xóa", "Tổng tin lưu", "ID hội thoại", "ID hồ sơ gốc"];
        for (var i = 0; i < detailHead.Length; i++) detail.Cell(6, i + 1).Value = detailHead[i];
        row = 7;
        foreach (var item in report.Rows)
        {
            detail.Cell(row, 1).Value = item.RecordCode;
            if (!string.IsNullOrEmpty(item.ColourCode)) detail.Cell(row, 2).Value = item.ColourCode;
            detail.Cell(row, 3).Value = item.Name;
            detail.Cell(row, 4).Value = item.IsActive ? "Có" : "Không";
            detail.Cell(row, 5).Value = item.Result;
            detail.Cell(row, 6).Value = item.VisibleMessages;
            detail.Cell(row, 7).Value = item.DeletedMessages;
            detail.Cell(row, 8).Value = item.VisibleMessages + item.DeletedMessages;
            detail.Cell(row, 9).Value = item.ConversationId.ToString("D");
            detail.Cell(row, 10).Value = item.RootConversationId.ToString("D");
            row++;
        }
        string[] memberHead = ["Mã hồ sơ", "Mã màu", "Tên nhóm", "EmployeeId President", "Kết quả", "Lý do", "ID hội thoại", "ID hồ sơ gốc"];
        for (var i = 0; i < memberHead.Length; i++) membership.Cell(6, i + 1).Value = memberHead[i];
        var groupInfo = report.Rows.ToDictionary(g => g.ConversationId);
        row = 7;
        foreach (var item in report.PresidentMemberships)
        {
            var info = groupInfo.GetValueOrDefault(item.ConversationId);
            membership.Cell(row, 1).Value = info?.RecordCode ?? "";
            membership.Cell(row, 2).Value = info?.ColourCode ?? "";
            membership.Cell(row, 3).Value = info?.Name ?? "";
            membership.Cell(row, 4).Value = item.EmployeeId.ToString("D");
            membership.Cell(row, 5).Value = item.Status switch
            {
                PresidentMembershipSyncStatus.Added => "Đã bổ sung",
                PresidentMembershipSyncStatus.Existing => "Đã có trước",
                PresidentMembershipSyncStatus.Removed => "Đã bị gỡ - giữ nguyên",
                PresidentMembershipSyncStatus.RolledBack => "Không áp dụng (rollback)",
                PresidentMembershipSyncStatus.Unknown => "Chưa xác nhận; chạy lại an toàn",
                _ => "Bỏ qua"
            };
            membership.Cell(row, 6).Value = item.Reason;
            membership.Cell(row, 7).Value = item.ConversationId.ToString("D");
            membership.Cell(row, 8).Value = item.RootConversationId.ToString("D");
            row++;
        }
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed()!;
            used.Style.Border.OutsideBorder = XLBorderStyleValues.None;
            used.Style.Border.InsideBorder = XLBorderStyleValues.None;
            sheet.Range(6, 1, 6, used.ColumnCount()).Style.Fill.BackgroundColor = XLColor.FromHtml("#244E7E");
            sheet.Range(6, 1, 6, used.ColumnCount()).Style.Font.FontColor = XLColor.White;
            sheet.Range(6, 1, 6, used.ColumnCount()).Style.Font.Bold = true;
            sheet.Row(6).Height = 28;
            sheet.Row(1).Height = 26;
            sheet.Range(2, 1, 2, used.ColumnCount()).Merge();
            sheet.Range(3, 1, 3, used.ColumnCount()).Merge();
            sheet.Range(4, 1, 4, used.ColumnCount()).Merge().Style.Alignment.WrapText = true;
            sheet.Row(4).Height = 34;
            sheet.Columns(1, used.ColumnCount()).Width = 21;
            sheet.Column(3).Width = 28;
            sheet.Columns(used.ColumnCount() - 1, used.ColumnCount()).Width = 39;
            if (used.LastRow().RowNumber() >= 7) sheet.Range(6, 1, used.LastRow().RowNumber(), used.ColumnCount()).CreateTable().Theme = XLTableTheme.TableStyleMedium2;
        }
        summary.Column(11).Width = 17;
        summary.Column(12).Width = 39;
        summary.Columns(13, 15).Width = 19;
        membership.Column(4).Width = 39;
        membership.Column(6).Width = 48;
        if (report.Rows.Count > 0)
        {
            summary.Range(7, 3, summary.LastRowUsed()!.RowNumber(), 11).Style.NumberFormat.Format = "#,##0";
            detail.Range(7, 6, detail.LastRowUsed()!.RowNumber(), 8).Style.NumberFormat.Format = "#,##0";
            summary.Range(7, 13, summary.LastRowUsed()!.RowNumber(), 15).Style.NumberFormat.Format = "#,##0";
        }
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
