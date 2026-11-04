using ClosedXML.Excel;
using HRM.Application.Features.PLM.Materials.TdsExtraction;

namespace HRM.Infrastructure.Services.Geminis.Materials;

internal sealed class TdsExcelRenderer : ITdsExcelRenderer
{
    public byte[] Render(IReadOnlyList<TdsFileResult> results)
    {
        using var workbook = new XLWorkbook();
        var documents = workbook.AddWorksheet("Documents");
        var specifications = workbook.AddWorksheet("Specifications");
        var review = workbook.AddWorksheet("Review");
        WriteRow(documents, 1, "File", "SHA256", "Status", "Duplicate of", "Normalization", "Document type", "Product",
            "Product code / grade", "Manufacturer", "Revision", "Issue date (source)", "Language", "Applications", "Storage",
            "Match status", "Material ID", "Material code", "Candidate codes", "Requires review");
        WriteRow(specifications, 1, "File", "Material code", "Specification", "Value / range (original)", "Unit",
            "Test method", "Conditions", "Source location", "Evidence (original)", "Requires review", "Validation codes");
        WriteRow(review, 1, "File", "Code", "Detail");
        var documentRow = 2;
        var specificationRow = 2;
        var reviewRow = 2;
        foreach (var result in results)
        {
            var data = result.Data;
            WriteRow(documents, documentRow++, result.FileName, result.Hash, result.Status, result.DuplicateOf,
                result.NormalizationMethod, data?.DocumentType, data?.ProductName, data?.ProductCode, data?.Manufacturer,
                data?.Revision, data?.IssueDate, data?.Language, data?.Applications, data?.Storage, result.MatchStatus,
                result.MaterialId?.ToString(), result.MaterialExternalId,
                string.Join("; ", result.Candidates.Select(x => x.ExternalId)), "Yes");
            foreach (var item in data?.Specifications ?? [])
                WriteRow(specifications, specificationRow++, result.FileName, result.MaterialExternalId,
                    item.Name, item.Value, item.Unit, item.TestMethod, item.Conditions, item.SourceLocation, item.Evidence, "Yes",
                    string.Join("; ", item.ValidationCodes));
            foreach (var warning in result.Warnings)
                WriteRow(review, reviewRow++, result.FileName, warning, "Check the original document before using the extracted data.");
            if (result.Status == "duplicate") WriteRow(review, reviewRow++, result.FileName, "duplicate_content", result.DuplicateOf);
            if (result.NormalizationMethod is "word_paragraphs_and_tables" or "excel_cached_values")
                WriteRow(review, reviewRow++, result.FileName, "office_text_only",
                    "Embedded images/charts are not extracted; Excel formulas use cached values and are not recalculated.");
        }
        foreach (var sheet in workbook.Worksheets)
        {
            var range = sheet.RangeUsed()!;
            range.SetAutoFilter();
            sheet.SheetView.FreezeRows(1);
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#17365D");
            sheet.Row(1).Style.Font.FontColor = XLColor.White;
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Height = 32;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            range.Style.Alignment.WrapText = true;
            sheet.Columns(1, range.ColumnCount()).Width = 24;
            sheet.Column(1).Width = 40;
            sheet.Rows(2, Math.Max(2, range.RowCount())).Height = 42;
        }
        specifications.Column(9).Width = 65;
        review.Column(3).Width = 75;
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params string?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            var value = values[column] ?? "";
            // Assign an explicit text value, never FormulaA1, including values beginning with =,+,-,@.
            sheet.Cell(row, column + 1).Value = value.Length <= 32767 ? value : value[..32740] + " [truncated for Excel]";
        }
    }
}
