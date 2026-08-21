namespace HRM.Infrastructure.Services.FileStorage;

public sealed class MaterialDocumentImportOptions
{
    public const string SectionName = "MaterialDocumentImport";

    public string SourceRoot { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = "TDS/MSDS NVL";
    public bool IncludeSubdirectories { get; set; } = true;
    public int MaxFiles { get; set; } = 10_000;
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf",
        ".jfif",
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".bmp",
        ".txt",
        ".csv",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".odt",
        ".ods"
    ];
}
