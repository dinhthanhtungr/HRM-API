using HRM.Domain.Enums.Devandqa;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;

/// <summary>Sửa danh mục; field bỏ qua/null giữ nguyên, field cấu hình nullable được clear theo whitelist.</summary>
public sealed record PatchEvaluationStandardCommand : IRequest<OperationResult<EvaluationStandardDto>>
{
    public Guid Id { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? NameEn { get; init; }
    public string? Description { get; init; }
    public bool? IsActive { get; init; }
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
