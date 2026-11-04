using System.Globalization;
using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Infrastructure.Documents.Pdfs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace HRM.Infrastructure.Documents.Excels.IQC;
/// <summary>Bảng kết quả theo mẫu Vietaus: logo, header vàng/xanh và các cột QC chỉ đọc.</summary>
public sealed class IqcEvaluationExcelRenderer(IOptions<PdfOptions> options, IHostEnvironment environment)
    : IIqcEvaluationExcelRenderer
{
    public byte[] Render(IReadOnlyList<IqcEvaluationExportColumn> columns)
    {
        if (columns.Count is < 1 or > 51) throw new ArgumentException("Số cột QC không hợp lệ.");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("IQC Results");
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Times New Roman";
        sheet.Style.Font.FontSize = 10;
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Column(1).Width = 30;
        sheet.Column(2).Width = 30;
        sheet.Column(3).Width = 10;
        sheet.Column(4).Width = 24;
        sheet.Row(1).Height = 62;
        sheet.Row(2).Height = 24;
        sheet.Range(1, 1, 1, 4).Merge();
        var configured = options.Value.Quotation.LogoPath;
        var logo = Path.Combine(environment.ContentRootPath,
            string.IsNullOrWhiteSpace(configured) ? "Assets/Pdf/VietAusLogo.png" : configured);
        if (!File.Exists(logo)) throw new InvalidOperationException("Không tìm thấy logo Vietaus đã cấu hình.");
        using var logoStream = File.OpenRead(logo);
        sheet.AddPicture(logoStream).MoveTo(sheet.Cell(1, 1), 42, 3).WithSize(85, 75);
        sheet.Cell(2, 1).Value = "Chỉ tiêu (Tiếng Việt)";
        sheet.Cell(2, 2).Value = "Test item (English)";
        sheet.Cell(2, 3).Value = "Unit";
        sheet.Cell(2, 4).Value = "Test method";
        for (var c = 0; c < columns.Count; c++)
        {
            var data = columns[c];
            var col = c + 5;
            sheet.Column(col).Width = 29;
            sheet.Cell(1, col).Value = string.Join("\n", new[] {
                data.QCCreatedDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                data.ProductCode, data.ProductName, data.LotNumber }.Where(x => !string.IsNullOrWhiteSpace(x)));
            sheet.Cell(1, col).Style.Fill.BackgroundColor = XLColor.Yellow;
            sheet.Cell(1, col).Style.Font.Bold = true;
            sheet.Cell(1, col).Style.Alignment.WrapText = true;
            sheet.Cell(1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(2, col).Value = "Results";
        }
        var maps = columns.Select(x => x.Evaluation.Results.ToDictionary(Key)).ToList();
        // Union để chỉ tiêu chỉ có ở lịch sử vẫn được xuất; không ghép theo vị trí mảng.
        var rows = columns.SelectMany(x => x.Evaluation.Results.OrderBy(r => r.Configuration.SortOrder))
            .DistinctBy(Key).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var source = rows[i];
            var row = i + 3;
            sheet.Row(row).Height = 30;
            sheet.Cell(row, 1).Value = source.CriterionName;
            sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(source.CriterionNameEn) ? "—" : source.CriterionNameEn;
            sheet.Cell(row, 3).Value = source.Configuration.Unit ?? "—";
            sheet.Cell(row, 4).Value = source.Configuration.TestMethod ?? "—";
            for (var c = 0; c < maps.Count; c++)
            {
                var cell = sheet.Cell(row, c + 5);
                if (!maps[c].TryGetValue(Key(source), out var result) || result.Value is null)
                    cell.Value = "—";
                else if (result.Configuration.DataType is EvaluationDataType.Decimal or EvaluationDataType.Integer &&
                    double.TryParse(result.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                {
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "0.############";
                }
                else cell.Value = result.Value;
                if (result is not null && (result.Configuration.Unit != source.Configuration.Unit ||
                    result.Configuration.TestMethod != source.Configuration.TestMethod || result.Configuration.DataType != source.Configuration.DataType))
                {
                    // Giữ khác biệt snapshot, không gán đơn vị/phương pháp mới cho kết quả cũ.
                    cell.Value = $"{result.Value ?? "—"}\n{result.Configuration.Unit} | {result.Configuration.TestMethod}";
                    sheet.Row(row).Height = 44;
                }
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }
        var lastColumn = columns.Count + 4;
        var range = sheet.Range(1, 1, Math.Max(2, rows.Count + 2), lastColumn);
        range.Style.Alignment.WrapText = true;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        var headers = sheet.Range(2, 1, 2, lastColumn);
        headers.Style.Fill.BackgroundColor = XLColor.FromHtml("#9BC2E6");
        headers.Style.Font.Bold = true;
        headers.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(2);
        sheet.SheetView.FreezeColumns(4);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 2);
        sheet.PageSetup.PrintAreas.Add(range.RangeAddress.ToString());
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
    private static string Key(EvaluationResultDto row) => row.EvaluationStandardId is { } id
        ? $"standard:{id}" : $"legacy:{row.CriterionCode}";
}
