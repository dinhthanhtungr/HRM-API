namespace HRM.Application.Abstractions.Documents;

/// <summary>Đọc bảng CSV/XLSX có giới hạn; không ghi DB hoặc truy cập đường dẫn ảnh trong bảng.</summary>
public interface ILabTestSheetReader
{
    LabTestSheet Read(byte[] content, string fileName);
}

public sealed record LabTestSheet(IReadOnlyList<string> Headers,
    IReadOnlyList<LabTestSheetRow> Rows, int IgnoredRows, IReadOnlyList<string> Warnings);
public sealed record LabTestSheetRow(int RowNumber, IReadOnlyList<string> Cells);
