using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.PreviewTemplateCatalogSync;

/// <summary>Xem trước đồng bộ các mã đã chọn; Items tùy chọn chứa draft FE chưa lưu.</summary>
public sealed record PreviewTemplateCatalogSyncQuery(Guid Id, IReadOnlyList<string> Codes,
    IReadOnlyList<EvaluationTemplateItemRequest>? Items = null) : IRequest<TemplateCatalogSyncPreviewDto>;
