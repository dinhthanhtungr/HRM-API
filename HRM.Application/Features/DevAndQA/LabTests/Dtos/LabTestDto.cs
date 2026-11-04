using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

public sealed record LabTestDto(Guid Id, string Code, DateOnly TestDate, string SampleName,
    string? SupplierCustomerName, string? BatchNumber, string? ItemCode, string? Resin,
    string? Application, string? PreparedByName, string? Note, decimal? WeightKg,
    string? ImportedPhotoName, Guid? EvaluationTemplateId, string? TemplateCode,
    string? TemplateName, int? TemplateVersion, DateTime CreatedDate, Guid CreatedBy,
    DateTime? UpdatedDate, Guid? UpdatedBy, IReadOnlyList<LabTestResultDto> Results);

public sealed record LabTestResultDto(Guid Id, Guid? EvaluationStandardId, string CriterionCode,
    string CriterionName, string? CriterionNameEn, EvaluationConfigurationDto Configuration,
    string? Value, string? ObservedText, Guid? AttachmentCollectionId);
