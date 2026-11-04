using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

/// <summary>Cấu hình đã lưu; phương pháp và yêu cầu không đọc lại từ danh mục hiện tại.</summary>
public sealed record EvaluationConfigurationDto(
    EvaluationDataType DataType, string? Unit, Guid? MeasurementMethodId, string? MeasurementMethodCode,
    string? TestMethod, string? InputConfigJson, string? AcceptanceRuleJson, string? SpecificationText,
    bool IsRequired, int SortOrder);
