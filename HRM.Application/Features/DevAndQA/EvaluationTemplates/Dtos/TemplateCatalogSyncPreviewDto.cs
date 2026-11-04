namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;

/// <summary>Dữ liệu trước/sau đồng bộ; chỉ xem trước, chưa ghi vào database.</summary>
public sealed record TemplateCatalogSyncPreviewDto(Guid TemplateId, IReadOnlyList<TemplateCatalogSyncChangeDto> Changes);
public sealed record TemplateCatalogSyncChangeDto(string Code, Guid EvaluationStandardId,
    EvaluationTemplateItemDto Before, EvaluationTemplateItemDto After);
