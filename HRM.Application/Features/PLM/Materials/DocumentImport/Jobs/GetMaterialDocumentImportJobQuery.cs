using HRM.Application.Abstractions.Security;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public sealed record GetMaterialDocumentImportJobQuery(Guid JobId)
    : IRequest<MaterialDocumentImportJobDto?>;

internal sealed class GetMaterialDocumentImportJobQueryHandler
    : IRequestHandler<GetMaterialDocumentImportJobQuery, MaterialDocumentImportJobDto?>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialDocumentImportJobQueue _jobQueue;

    public GetMaterialDocumentImportJobQueryHandler(
        ICurrentUser currentUser,
        IMaterialDocumentImportJobQueue jobQueue)
    {
        _currentUser = currentUser;
        _jobQueue = jobQueue;
    }

    public Task<MaterialDocumentImportJobDto?> Handle(
        GetMaterialDocumentImportJobQuery request,
        CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return Task.FromResult<MaterialDocumentImportJobDto?>(null);
        }

        var job = _jobQueue.Get(request.JobId, companyId);
        return Task.FromResult(job is null
            ? null
            : MaterialDocumentImportJobMapper.ToDto(job));
    }
}
