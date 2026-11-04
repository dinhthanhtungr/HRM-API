using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using Microsoft.VisualBasic.FileIO;

namespace HRM.Infrastructure.Documents.Excels.LabTests;

/// <summary>Đọc CSV multiline hoặc XLSX với giới hạn bộ nhớ; không thực thi công thức/đọc ảnh từ đường dẫn.</summary>
public sealed class LabTestSheetReader : ILabTestSheetReader
{
    public const int MaxFileBytes = 5_000_000;
    private const int MaxRows = 10_000;
    private const int MaxColumns = 100;

    public LabTestSheet Read(byte[] content, string fileName)
    {
        if (content.Length is 0 or > MaxFileBytes) throw new EvaluationRequestException("File phải từ 1 byte đến 5 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".csv" => ReadCsv(content),
                ".xlsx" => ReadXlsx(content),
                _ => throw new EvaluationRequestException("Chỉ hỗ trợ CSV hoặc XLSX.")
            };
        }
        catch (EvaluationRequestException) { throw; }
        catch (Exception ex) when (ex is IOException or FormatException or ArgumentException or MalformedLineException or XmlException or OverflowException)
        {
            throw new EvaluationRequestException("File không hợp lệ; kiểm tra định dạng CSV/XLSX.", 400, ex);
        }
    }

    private static LabTestSheet ReadCsv(byte[] content)
    {
        var warnings = new List<string>();
        string text;
        try { text = new UTF8Encoding(false, true).GetString(content).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(1258).GetString(content);
            warnings.Add("CSV không phải UTF-8; đã đọc Windows-1258. Chữ đã mất thành '?' trong nguồn không thể tự phục hồi.");
        }
        if (text.Contains('\0')) throw new EvaluationRequestException("CSV chứa dữ liệu nhị phân.");
        using var parser = new TextFieldParser(new StringReader(text))
        { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        var rows = new List<IReadOnlyList<string>>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields() ?? [];
            ValidateSize(rows.Count + 1, row.Length);
            if (row.Any(x => x.Length > 16000)) throw new EvaluationRequestException("Ô dữ liệu vượt 16000 ký tự.");
            rows.Add(row);
        }
        return Table(rows, warnings);
    }

    private static LabTestSheet ReadXlsx(byte[] content)
    {
        using (var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read))
        {
            if (archive.Entries.Count > 1000 || archive.Entries.Sum(x => x.Length) > 50_000_000 ||
                archive.Entries.Any(x => x.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase)))
                throw new EvaluationRequestException("XLSX quá lớn sau giải nén hoặc có liên kết ngoài.");
        }
        using var workbook = new XLWorkbook(new MemoryStream(content));
        if (workbook.Worksheets.Count != 1)
            throw new EvaluationRequestException("File import cần đúng một sheet dữ liệu.");
        var sheet = workbook.Worksheets.First();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        ValidateSize(lastRow, lastColumn);
        if ((long)lastRow * lastColumn > 300_000) throw new EvaluationRequestException("XLSX vượt 300000 ô.");
        var rows = new List<IReadOnlyList<string>>();
        for (var row = 1; row <= lastRow; row++)
        {
            var cells = new List<string>();
            for (var col = 1; col <= lastColumn; col++)
            {
                var cell = sheet.Cell(row, col);
                if (cell.HasFormula) throw new EvaluationRequestException($"Ô {cell.Address} có công thức; chuyển sang giá trị trước import.");
                var value = cell.DataType switch
                {
                    XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    XLDataType.Number => ((decimal)cell.GetDouble()).ToString("G29", CultureInfo.InvariantCulture),
                    XLDataType.Boolean => cell.GetBoolean() ? "true" : "false",
                    _ => cell.GetString()
                };
                if (value.Length > 16000) throw new EvaluationRequestException("Ô dữ liệu vượt 16000 ký tự.");
                cells.Add(value);
            }
            rows.Add(cells);
        }
        return Table(rows, []);
    }

    private static LabTestSheet Table(List<IReadOnlyList<string>> rows, List<string> warnings)
    {
        var headerIndex = rows.FindIndex(x => x.Any(c => c.Trim().Equals("TT", StringComparison.OrdinalIgnoreCase)) &&
            x.Any(c => c.Trim().StartsWith("DATE", StringComparison.OrdinalIgnoreCase)));
        if (headerIndex < 0 || headerIndex > 10) throw new EvaluationRequestException("Không tìm thấy hàng tiêu đề TT/DATE trong 11 hàng đầu.");
        var headers = rows[headerIndex].Select((x, i) => string.IsNullOrWhiteSpace(x) ? $"unnamed:{i + 1}" : x.Trim()).ToArray();
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new EvaluationRequestException("Tên cột bị trùng.");
        var data = new List<LabTestSheetRow>();
        var ignored = 0;
        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count > headers.Length) throw new EvaluationRequestException($"Dòng {i + 1} vượt số cột tiêu đề.");
            // STT điền sẵn không tạo phiếu trống.
            if (!row.Skip(1).Any(x => !string.IsNullOrWhiteSpace(x))) { ignored++; continue; }
            data.Add(new(i + 1, row));
        }
        return new(headers, data, ignored, warnings);
    }

    private static void ValidateSize(int rows, int columns)
    {
        if (rows > MaxRows || columns > MaxColumns) throw new EvaluationRequestException("File vượt 10000 hàng hoặc 100 cột.");
    }
}
