using HRM.Domain.Enums.Devandqa;

using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;

public sealed record EvaluationTemplateItemDto(Guid Id, Guid EvaluationStandardId, string CriterionCode,
    string CriterionName, EvaluationConfigurationDto Configuration, string? CriterionNameEn = null);
public sealed record EvaluationTemplateDto(Guid Id, string Code, string Name, EvaluationTemplateType Type,
    int Version, EvaluationTemplateStatus Status, bool IsDefault, DateTime? EffectiveFrom,
    Guid CreatedBy, DateTime CreatedDate, Guid? PublishedBy, DateTime? PublishedDate,
    IReadOnlyList<EvaluationTemplateItemDto> Items);
