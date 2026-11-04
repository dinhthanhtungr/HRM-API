using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;

public sealed record EvaluationTemplateItemRequest
{
    public Guid EvaluationStandardId { get; init; }
    public EvaluationConfigurationRequest? Configuration { get; init; }
}
