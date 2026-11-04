using ClosedXML.Excel;
using HRM.Application.Features.DevAndQA.IQC.Dtos;

namespace HRM.Infrastructure.Documents.Excels.IQC;

public sealed partial class IqcExcelRenderer
{
    private void BuildReportSheet(
        XLWorkbook wb,
        List<QCInputCategoryReportRow> categoryRows,
        List<QCInputQcStatusReportRow> qcRows)
    {
        var ws = wb.Worksheets.Add("BaoCao");

        // title tổng
        ws.Cell(1, 1).Value = "BÁO CÁO QC NGUYÊN LIỆU";
        ws.Range(1, 1, 1, 10).Merge();
        ws.Range(1, 1, 1, 10).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 10).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(1, 1, 1, 10).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // ===== block trái =====
        ws.Cell(3, 1).Value = "BẢNG THỐNG KÊ TỪNG LOẠI NGUYÊN LIỆU NHẬP VỀ";
        ws.Range(3, 1, 3, 4).Merge();
        StyleSectionTitle(ws.Range(3, 1, 3, 4));

        ws.Cell(5, 1).Value = "PHÂN LOẠI";
        ws.Cell(5, 2).Value = "SỐ MÃ";
        ws.Cell(5, 3).Value = "SỐ LƯỢNG (kg)";
        ws.Cell(5, 4).Value = "TỶ LỆ (%)";
        StyleHeader(ws.Range(5, 1, 5, 4));

        var leftStartRow = 6;
        var leftRow = leftStartRow;
        foreach (var row in categoryRows)
        {
            ws.Cell(leftRow, 1).Value = row.CategoryName;
            ws.Cell(leftRow, 2).Value = row.ItemCount;
            ws.Cell(leftRow, 3).Value = row.QtyKg;
            ws.Cell(leftRow, 4).Value = row.Percent;
            leftRow++;
        }

        var leftTotalRow = leftRow;
        ws.Cell(leftTotalRow, 1).Value = "TỔNG";
        ws.Cell(leftTotalRow, 2).Value = categoryRows.Sum(x => x.ItemCount);
        ws.Cell(leftTotalRow, 3).Value = categoryRows.Sum(x => x.QtyKg);
        ws.Cell(leftTotalRow, 4).Value = 100m;
        StyleTotalRow(ws.Range(leftTotalRow, 1, leftTotalRow, 4));

        if (leftTotalRow >= leftStartRow)
        {
            var leftRange = ws.Range(6, 1, leftTotalRow, 4);
            leftRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            leftRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            leftRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // ===== block phải =====
        ws.Cell(3, 6).Value = "BẢNG THỂ HIỆN KẾT QUẢ QC NGUYÊN LIỆU";
        ws.Range(3, 6, 3, 9).Merge();
        StyleSectionTitle(ws.Range(3, 6, 3, 9));

        ws.Cell(5, 6).Value = "KẾT QUẢ";
        ws.Cell(5, 7).Value = "SỐ MÃ";
        ws.Cell(5, 8).Value = "SỐ LƯỢNG (kg)";
        ws.Cell(5, 9).Value = "TỶ LỆ (%)";
        StyleHeader(ws.Range(5, 6, 5, 9));

        var rightStartRow = 6;
        var rightRow = rightStartRow;
        foreach (var row in qcRows)
        {
            ws.Cell(rightRow, 6).Value = row.StatusName;
            ws.Cell(rightRow, 7).Value = row.ItemCount;
            ws.Cell(rightRow, 8).Value = row.QtyKg;
            ws.Cell(rightRow, 9).Value = row.Percent;
            rightRow++;
        }

        var rightTotalRow = rightRow;
        ws.Cell(rightTotalRow, 6).Value = "TỔNG";
        ws.Cell(rightTotalRow, 7).Value = qcRows.Sum(x => x.ItemCount);
        ws.Cell(rightTotalRow, 8).Value = qcRows.Sum(x => x.QtyKg);
        ws.Cell(rightTotalRow, 9).Value = 100m;
        StyleTotalRow(ws.Range(rightTotalRow, 6, rightTotalRow, 9));

        if (rightTotalRow >= rightStartRow)
        {
            var rightRange = ws.Range(6, 6, rightTotalRow, 9);
            rightRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rightRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            rightRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // format cột
        ws.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Column(3).Style.NumberFormat.Format = "#,##0.0";
        ws.Column(4).Style.NumberFormat.Format = "#,##0.0";

        ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Column(8).Style.NumberFormat.Format = "#,##0.0";
        ws.Column(9).Style.NumberFormat.Format = "#,##0.0";

        // width
        ws.Column(1).Width = 28;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 20;
        ws.Column(4).Width = 14;

        ws.Column(5).Width = 4; // cột trống ngăn cách

        ws.Column(6).Width = 24;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 20;
        ws.Column(9).Width = 14;

        ws.SheetView.FreezeRows(5);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }


}
