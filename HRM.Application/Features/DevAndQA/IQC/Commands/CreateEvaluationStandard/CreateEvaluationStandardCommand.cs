using HRM.Domain.Enums.Devandqa;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;

/// <summary>Tạo tiêu chuẩn đánh giá trong company hiện tại; company/audit lấy từ server.</summary>
public sealed record CreateEvaluationStandardCommand : IRequest<OperationResult<EvaluationStandardDto>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public EvaluationDataType DataType { get; init; } = EvaluationDataType.Text;
    public string? Unit { get; init; }
    public Guid? MeasurementMethodId { get; init; }
    public string? TestMethod { get; init; }
    public string? InputConfigJson { get; init; }
    public string? AcceptanceRuleJson { get; init; }
    public string? SpecificationText { get; init; }
    public bool IsRequired { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int SortOrder { get; init; }
}
