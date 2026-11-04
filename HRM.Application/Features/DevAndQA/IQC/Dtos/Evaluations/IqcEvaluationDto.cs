using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;

public sealed record IqcEvaluationDto(Guid? Id, Guid? QCInputByQCId, Guid? EvaluationTemplateId,
    string? TemplateCode, string? TemplateName, int? TemplateVersion, IReadOnlyList<EvaluationResultDto> Results,
    bool IsStored = true);
