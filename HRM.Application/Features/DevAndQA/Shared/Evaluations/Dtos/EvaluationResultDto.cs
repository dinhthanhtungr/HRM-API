namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

public sealed record EvaluationResultDto(Guid Id, Guid? EvaluationStandardId, Guid? SourceTemplateItemId,
    string ExternalId, string CriterionCode, string CriterionName, bool HasConfigurationSnapshot,
    string? Value, EvaluationConfigurationDto Configuration, string? CriterionNameEn = null, Guid? AttachmentCollectionId = null);
