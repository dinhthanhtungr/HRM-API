using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace HRM.Infrastructure.Services.Geminis.Materials;

internal sealed class TdsDocumentNormalizer(IOptions<TdsExtractionOptions> options,
    TdsLegacyOfficeConverter legacyConverter) : ITdsDocumentNormalizer
{
    public async Task<TdsNormalizedDocument> NormalizeAsync(TdsInputFile file, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        TdsExtractionRules.ValidateFile(file.FileName, file.Content.Length, settings);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        try
        {
            if (extension == ".pdf") return Pdf(file.Content, "pdf_native");
            if (extension is ".doc" or ".xls")
                return Pdf(await legacyConverter.ConvertAsync(file, cancellationToken), "legacy_office_to_pdf");
            if (extension == ".docx") return Text(ReadWord(file.Content, cancellationToken), "word_paragraphs_and_tables");
            if (extension == ".xlsx") return Text(ReadExcel(file.Content, cancellationToken), "excel_cached_values");
            if (extension is ".txt" or ".csv")
                return Text(new UTF8Encoding(false, true).GetString(file.Content).TrimStart('\uFEFF'), "utf8_text");
            using var input = new MemoryStream(file.Content, writable: false);
            var info = await Image.IdentifyAsync(input, cancellationToken);
            if ((long)info.Width * info.Height > 40_000_000) throw new TdsProcessingException("image_pixel_limit");
            input.Position = 0;
            using var image = await Image.LoadAsync(input, cancellationToken);
            if (image.Frames.Count != 1) throw new TdsProcessingException("multi_frame_image_requires_split");
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;
            using var output = new MemoryStream();
            await image.SaveAsPngAsync(output, cancellationToken);
            if (output.Length > settings.MaxFileBytes) throw new TdsProcessingException("normalized_file_size_limit");
            return new("image/png", output.ToArray(), null, "image_to_png");
        }
        catch (TdsProcessingException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new TdsProcessingException("invalid_or_encrypted_document"); }
    }

    private TdsNormalizedDocument Text(string text, string method)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new TdsProcessingException("document_text_empty");
        if (text.Length > options.Value.MaxTextCharacters) throw new TdsProcessingException("document_text_limit");
        return new("text/plain", null, text, method);
    }

    private static TdsNormalizedDocument Pdf(byte[] bytes, string method)
    {
        if (bytes.Length < 5 || !Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024)).Contains("%PDF-"))
            throw new TdsProcessingException("invalid_pdf_signature");
        return new("application/pdf", bytes, null, method);
    }

    private static void ValidateArchive(ZipArchive archive, CancellationToken cancellationToken)
    {
        if (archive.Entries.Count > 3000 || archive.Entries.Sum(x => x.Length) > 40 * 1024 * 1024)
            throw new TdsProcessingException("office_archive_limit");
        if (archive.Entries.Any(x => x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
            throw new TdsProcessingException("macro_document_not_supported");
        // Check actual expanded bytes as well as ZIP metadata; a hostile archive can lie about its size.
        long expanded = 0;
        var buffer = new byte[81920];
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                expanded += read;
                if (expanded > 40 * 1024 * 1024) throw new TdsProcessingException("office_archive_limit");
            }
        }
    }

    private string ReadWord(byte[] bytes, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        ValidateArchive(archive, cancellationToken);
        var entry = archive.GetEntry("word/document.xml") ?? throw new TdsProcessingException("invalid_word_document");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 40 * 1024 * 1024 });
        var xml = XDocument.Load(reader);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var output = new StringBuilder();
        var paragraph = 0;
        var tables = xml.Descendants(word + "tbl").Select((table, index) => (table, index: index + 1))
            .ToDictionary(x => x.table, x => x.index);
        foreach (var element in xml.Descendants(word + "p"))
        {
            var text = string.Concat(element.Descendants().Select(x => x.Name == word + "t" ? x.Value :
                x.Name == word + "tab" ? "\t" : x.Name == word + "br" ? "\n" : ""));
            var location = $"Paragraph {++paragraph}";
            var cell = element.Ancestors(word + "tc").FirstOrDefault();
            var row = cell?.Parent;
            var table = row?.Parent;
            if (cell is not null && row is not null && table is not null && tables.TryGetValue(table, out var tableIndex))
                location += $"; Table {tableIndex} Row {row.ElementsBeforeSelf(word + "tr").Count() + 1} Cell {cell.ElementsBeforeSelf(word + "tc").Count() + 1}";
            output.AppendLine($"[{location}] {text}");
            CheckTextLimit(output);
        }
        return output.ToString();
    }

    private string ReadExcel(byte[] bytes, CancellationToken cancellationToken)
    {
        using (var archive = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read))
            ValidateArchive(archive, cancellationToken);
        using var input = new MemoryStream(bytes, writable: false);
        using var workbook = new XLWorkbook(input);
        using var formattingWorkbook = new XLWorkbook();
        var formattingCell = formattingWorkbook.AddWorksheet("Formatting").Cell("A1");
        var output = new StringBuilder();
        foreach (var sheet in workbook.Worksheets)
        {
            output.AppendLine($"[Sheet: {sheet.Name}]");
            foreach (var row in sheet.RowsUsed())
            {
                foreach (var cell in row.CellsUsed())
                {
                    // Never execute workbook formulas or follow external links during extraction.
                    var cached = cell.CachedValue;
                    formattingCell.Value = cached;
                    formattingCell.Style = cell.Style;
                    var value = formattingCell.GetFormattedString(System.Globalization.CultureInfo.InvariantCulture);
                    if (cell.HasFormula && cached.Type == XLDataType.Blank) value = "[cached formula value missing]";
                    output.AppendLine($"[{sheet.Name}!{cell.Address}] {value}");
                    CheckTextLimit(output);
                }
            }
        }
        return output.ToString();
    }

    private void CheckTextLimit(StringBuilder text)
    {
        if (text.Length > options.Value.MaxTextCharacters) throw new TdsProcessingException("document_text_limit");
    }
}
