using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using HRM.Infrastructure.Services.Geminis.Materials;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class TdsDocumentNormalizerTests
{
    [Fact]
    public async Task WordPreservesParagraphAndTableTextAndDoesNotInventPages()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>
                <w:p><w:r><w:t>TDS Resin</w:t></w:r></w:p>
                <w:tbl><w:tr><w:tc><w:p><w:r><w:t>Density</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>1.2 g/cm³</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                </w:body></w:document>
                """);
        }
        var normalized = await CreateNormalizer().NormalizeAsync(new("tds.docx", output.ToArray()), default);
        Assert.Equal("word_paragraphs_and_tables", normalized.Method);
        Assert.Contains("[Paragraph 1] TDS Resin", normalized.Text);
        Assert.Contains("[Paragraph 3; Table 1 Row 1 Cell 2] 1.2 g/cm³", normalized.Text);
        Assert.DoesNotContain("Page", normalized.Text);
    }

    [Fact]
    public async Task ExcelIncludesSheetAndCellLabelsWithoutCalculatingFormulas()
    {
        using var output = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("TDS");
            sheet.Cell("A1").Value = "Density";
            sheet.Cell("B1").Value = "1.2–1.5 g/cm³";
            sheet.Cell("C1").FormulaA1 = "1/0";
            sheet.Cell("D1").Value = 0.25;
            sheet.Cell("D1").Style.NumberFormat.Format = "0%";
            workbook.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = false });
        }
        var normalized = await CreateNormalizer().NormalizeAsync(new("tds.xlsx", output.ToArray()), default);
        Assert.Contains("[TDS!A1] Density", normalized.Text);
        Assert.Contains("[TDS!B1] 1.2–1.5 g/cm³", normalized.Text);
        Assert.DoesNotContain("#DIV/0!", normalized.Text);
        Assert.Contains("[TDS!D1] 25%", normalized.Text);
        Assert.Contains("[cached formula value missing]", normalized.Text);
    }

    [Fact]
    public async Task ImageNormalizesToPngAndRejectsDisguisedText()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var output = new MemoryStream();
        image.SaveAsJpeg(output);
        var normalizer = CreateNormalizer();
        var normalized = await normalizer.NormalizeAsync(new("tds.jpg", output.ToArray()), default);
        Assert.Equal("image/png", normalized.MimeType);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, normalized.Binary![..4]);
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() =>
            normalizer.NormalizeAsync(new("tds.jpg", Encoding.UTF8.GetBytes("not an image")), default));
        Assert.Equal("invalid_or_encrypted_document", error.Code);
    }

    [Theory]
    [InlineData("tds.doc")]
    [InlineData("tds.xls")]
    public async Task LegacyOfficeWithoutConfiguredConverterFailsClearly(string name)
    {
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() => CreateNormalizer().NormalizeAsync(new(name, [1]), default));
        Assert.Equal("legacy_office_converter_not_configured", error.Code);
    }

    [Fact]
    public async Task OversizedTextIsRejectedRatherThanSilentlyTruncated()
    {
        var normalizer = CreateNormalizer(new() { MaxTextCharacters = 100 });
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() => normalizer.NormalizeAsync(new("tds.txt", new byte[101].Select(_ => (byte)65).ToArray()), default));
        Assert.Equal("document_text_limit", error.Code);
    }

    [Fact]
    public void ExportKeepsLiteralTextZeroAndRangesAndProvidesEvidenceAndReview()
    {
        var result = new TdsFileResult { FileName = "=HYPERLINK(\"evil\")", Status = "extracted", Data = new()
            { ProductName = "=1+1", Specifications = [new() { Name = "Density", Value = "0", Unit = "g/cm³",
                Conditions = "25°C", Evidence = "0 g/cm³", SourceLocation = "Page 2" },
                new() { Name = "Viscosity", Value = "1,000–1,500", Unit = "mPa·s" }] }, Warnings = ["ai_output_requires_review"] };
        var bytes = new TdsExcelRenderer().Render([result]);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(4, workbook.Worksheets.Count);
        Assert.False(workbook.Worksheet("Documents").Cell("A2").HasFormula);
        Assert.Equal("=1+1", workbook.Worksheet("Documents").Cell("G2").GetString());
        Assert.False(workbook.Worksheet("Documents").Cell("G2").HasFormula);
        Assert.Equal("0", workbook.Worksheet("Specifications").Cell("D2").GetString());
        Assert.Equal("Page 2", workbook.Worksheet("Specifications").Cell("H2").GetString());
        Assert.Equal("1,000–1,500", workbook.Worksheet("Specifications").Cell("D3").GetString());
        Assert.Equal("ai_output_requires_review", workbook.Worksheet("Review").Cell("B2").GetString());
    }

    private static TdsDocumentNormalizer CreateNormalizer(TdsExtractionOptions? options = null)
    {
        var settings = Options.Create(options ?? new());
        return new(settings, new TdsLegacyOfficeConverter(settings));
    }
}
