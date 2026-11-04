using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.SyncIqcResultsFromCatalog;

internal sealed class SyncIqcResultsFromCatalogCommandHandler(IqcResultCatalogSyncService service)
    : IRequestHandler<SyncIqcResultsFromCatalogCommand, IqcResultCatalogSyncDto>
{
    public Task<IqcResultCatalogSyncDto> Handle(SyncIqcResultsFromCatalogCommand request, CancellationToken ct) =>
        service.ApplyAsync(request.Selection, request.PreviewToken, ct);
}
