using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;

/// <summary>Tiêu chuẩn đánh giá trong danh mục IQC; không trả navigation EF.</summary>
public sealed record EvaluationStandardDto(Guid Id, string Code, string Name, string? Description,
    bool IsActive, int SortOrder, DateTime CreatedDate, Guid CreatedBy, DateTime? UpdatedDate, Guid? UpdatedBy,
    EvaluationDataType DataType, string? Unit, string? TestMethod, string? InputConfigJson,
    string? AcceptanceRuleJson, string? SpecificationText, bool IsRequired, Guid? MeasurementMethodId = null, string? NameEn = null);
