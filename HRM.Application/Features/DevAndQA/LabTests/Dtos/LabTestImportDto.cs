namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

public sealed record LabTestImportPreviewDto(Guid ImportBatchId, int TotalDataRows,
    int IgnoredRows, IReadOnlyList<string> Headers, IReadOnlyList<string> Warnings,
    IReadOnlyList<LabTestImportPreviewRowDto> Rows, IReadOnlyDictionary<string, Guid> ColumnStandards);

public sealed record LabTestImportPreviewRowDto(int RowNumber, LabTestWriteRequest Data,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public sealed record LabTestImportRow(int RowNumber, LabTestWriteRequest Data);

public sealed record LabTestImportResultDto(int Created, int Skipped,
    IReadOnlyList<LabTestImportCreatedDto> Items);

public sealed record LabTestImportCreatedDto(int RowNumber, Guid Id, string Code, bool AlreadyImported);
