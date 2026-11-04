using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

/// <summary>PATCH cấu hình: null/bỏ qua giữ nguyên; xóa field nullable bằng clearFields.</summary>
public sealed record EvaluationConfigurationRequest
{
    public EvaluationDataType? DataType { get; init; }
    public string? Unit { get; init; }
    public Guid? MeasurementMethodId { get; init; }
    public string? TestMethod { get; init; }
    public string? InputConfigJson { get; init; }
    public string? AcceptanceRuleJson { get; init; }
    public string? SpecificationText { get; init; }
    public bool? IsRequired { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? SortOrder { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
}
