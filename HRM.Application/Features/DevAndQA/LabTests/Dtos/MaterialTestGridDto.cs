using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

public enum MaterialTestSource { Iqc = 1, LabTest = 2 }

public sealed record MaterialTestGridDto(PagedResult<MaterialTestRowDto> Page,
    IReadOnlyList<MaterialTestColumnDto> Columns);

public sealed record MaterialTestColumnDto(string Key, string Code, string Name, string? NameEn,
    EvaluationDataType DataType, string? Unit, int SortOrder);

public sealed record MaterialTestCellDto(string Key, Guid ResultId, Guid? EvaluationStandardId,
    string? Value, string? ObservedText, EvaluationConfigurationDto Configuration, Guid? AttachmentCollectionId);

public sealed record MaterialTestRowDto(MaterialTestSource Source, Guid Id, long? VoucherDetailId,
    DateOnly TestDate, string? MaterialCode, string? ItemCode, string SampleName,
    string? SupplierCustomerName, string? BatchNumber, string? Resin, string? Application,
    string? PreparedByName, string? Note, decimal? WeightKg, string? ImportedPhotoName,
    bool CanEdit, bool CanDelete, IReadOnlyList<MaterialTestCellDto> Cells);
