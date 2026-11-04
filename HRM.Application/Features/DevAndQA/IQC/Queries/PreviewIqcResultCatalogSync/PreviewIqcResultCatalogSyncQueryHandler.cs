using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.PreviewIqcResultCatalogSync;

internal sealed class PreviewIqcResultCatalogSyncQueryHandler(IqcResultCatalogSyncService service)
    : IRequestHandler<PreviewIqcResultCatalogSyncQuery, IqcResultCatalogSyncDto>
{
    public Task<IqcResultCatalogSyncDto> Handle(PreviewIqcResultCatalogSyncQuery request, CancellationToken ct) =>
        service.PreviewAsync(request.Selection, ct);
}
