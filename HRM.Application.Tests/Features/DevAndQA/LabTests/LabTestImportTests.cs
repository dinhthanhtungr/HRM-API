using System.Text;
using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Infrastructure.Documents.Excels.LabTests;

namespace HRM.Application.Tests.Features.DevAndQA.LabTests;

public sealed class LabTestImportTests
{
    private static readonly string[] Headers = ["TT", "DATE\nNgày, tháng, năm", "NCC/KH", "BATCH NO", "Mã hàng", "Tên hàng", "RESIN",
        "Density (g/cm3)", "Tensile strength (Mpa)", "Elongation at Break (%)", "Flexural strength (Mpa)", "Flexural Modulus (Mpa)",
        "Izod Impact strength (KJ/m2)", "Melt flow rate (g/10min)", "Độ ẩm", "Ash (%)", "Rohs", "Ultimate stress (Mpa)",
        "Break Strain (%)", "Secant Modulus (Mpa)", "Application", "REPEARED BY", "Note", "Sample Photo", "Khối lượng (KG)", ""];

    [Fact]
    public void CsvHandlesMultilineQuotesAndSkipsNumberOnlyRows()
    {
        var row = new string[26]; row[0] = "1"; row[1] = "27 Thg11 23"; row[5] = "Sample, one"; row[7] = "0.9";
        var numberOnly = new string[26]; numberOnly[0] = "2";
        string Line(IEnumerable<string> cells) => string.Join(",", cells.Select(x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\""));
        var content = Encoding.UTF8.GetBytes("Title\n" + Line(Headers) + "\n" + Line(row) + "\n" + Line(numberOnly));
        var sheet = new LabTestSheetReader().Read(content, "tests.csv");
        Assert.Single(sheet.Rows);
        Assert.Equal("Sample, one", sheet.Rows[0].Cells[5]);
        Assert.Equal(1, sheet.IgnoredRows);
        Assert.Equal(new DateOnly(2023, 11, 27), LabTestImportMapping.ParseDate(sheet.Rows[0].Cells[1]));
    }

    [Fact]
    public void MappingPreservesRangesNotesAndPhotoNameWithoutReadingFiles()
    {
        var row = new string[26]; row[0] = "1"; row[1] = "2026-01-02"; row[5] = "PP"; row[7] = "0.95-0.97";
        row[23] = "../untrusted/folder/photo.jpg"; row[24] = "12,5k"; row[25] = "17.5";
        var sheet = new LabTestSheet(Headers.Select((x, i) => x == "" ? $"unnamed:{i + 1}" : x).ToArray(), [], 0, []);
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), DataType = EvaluationDataType.Decimal };
        var mapped = LabTestImportMapping.Map(sheet, new(5, row), new Dictionary<string, EvaluationStandard> { [Headers[7]] = standard });
        Assert.Empty(mapped.Errors);
        Assert.Null(Assert.Single(mapped.Data.Results!).Value);
        Assert.Equal("0.95-0.97", mapped.Data.Results![0].ObservedText);
        Assert.Equal("photo.jpg", mapped.Data.ImportedPhotoName);
        Assert.Contains("12,5k", mapped.Data.Note);
        Assert.Contains("17.5", mapped.Data.Note);
        Assert.Null(mapped.Data.WeightKg);
    }

    [Fact]
    public void UnmappedNonemptyCriterionProducesRowError()
    {
        var row = new string[26]; row[1] = "2026-01-02"; row[5] = "PP"; row[7] = "0.9";
        var mapped = LabTestImportMapping.Map(new(Headers, [], 0, []), new(5, row), new Dictionary<string, EvaluationStandard>());
        Assert.Contains(mapped.Errors, x => x.Contains("Density"));
    }

    [Fact]
    public void XlsxDatesAndNumbersAreReadWithoutEvaluatingFormulas()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        for (var i = 0; i < Headers.Length; i++) sheet.Cell(1, i + 1).Value = Headers[i];
        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = new DateTime(2026, 1, 2);
        sheet.Cell(2, 6).Value = "PP";
        sheet.Cell(2, 8).Value = 0.9;
        using var buffer = new MemoryStream(); workbook.SaveAs(buffer);
        Assert.Equal("2026-01-02", new LabTestSheetReader().Read(buffer.ToArray(), "tests.xlsx").Rows[0].Cells[1]);
        sheet.Cell(2, 8).FormulaA1 = "1/2";
        using var formulaBuffer = new MemoryStream(); workbook.SaveAs(formulaBuffer);
        Assert.Throws<EvaluationRequestException>(() => new LabTestSheetReader().Read(formulaBuffer.ToArray(), "tests.xlsx"));
    }

    [Fact]
    public void BinaryAndOversizedInputsAreRejected()
    {
        var reader = new LabTestSheetReader();
        Assert.Throws<EvaluationRequestException>(() => reader.Read(new byte[5_000_001], "large.csv"));
        Assert.Throws<EvaluationRequestException>(() => reader.Read([0, 1, 2], "binary.csv"));
        Assert.Throws<EvaluationRequestException>(() => reader.Read([1, 2, 3], "legacy.xls"));
    }
}
