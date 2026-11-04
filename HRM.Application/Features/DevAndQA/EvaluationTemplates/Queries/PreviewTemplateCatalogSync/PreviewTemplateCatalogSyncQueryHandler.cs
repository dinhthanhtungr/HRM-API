using HRM.Application.Features.DevAndQA.EvaluationTemplates.Dtos;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.EvaluationTemplates.Queries.PreviewTemplateCatalogSync;

internal sealed class PreviewTemplateCatalogSyncQueryHandler(EvaluationTemplateService service)
    : IRequestHandler<PreviewTemplateCatalogSyncQuery, TemplateCatalogSyncPreviewDto>
{
    public Task<TemplateCatalogSyncPreviewDto> Handle(PreviewTemplateCatalogSyncQuery request, CancellationToken ct) =>
        service.PreviewCatalogSyncAsync(request, ct);
}
