using HRM.Application.Abstractions.Security;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public sealed record GetMaterialDocumentImportJobExceptionsQuery(Guid JobId)
    : IRequest<IReadOnlyList<MaterialDocumentImportJobExceptionDto>?>;

internal sealed class GetMaterialDocumentImportJobExceptionsQueryHandler
    : IRequestHandler<GetMaterialDocumentImportJobExceptionsQuery, IReadOnlyList<MaterialDocumentImportJobExceptionDto>?>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialDocumentImportJobQueue _jobQueue;

    public GetMaterialDocumentImportJobExceptionsQueryHandler(
        ICurrentUser currentUser,
        IMaterialDocumentImportJobQueue jobQueue)
    {
        _currentUser = currentUser;
        _jobQueue = jobQueue;
    }

    public Task<IReadOnlyList<MaterialDocumentImportJobExceptionDto>?> Handle(
        GetMaterialDocumentImportJobExceptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _jobQueue.Get(request.JobId, companyId) is null)
        {
            return Task.FromResult<IReadOnlyList<MaterialDocumentImportJobExceptionDto>?>(null);
        }

        IReadOnlyList<MaterialDocumentImportJobExceptionDto> result = _jobQueue
            .GetExceptions(request.JobId, companyId)
            .Select(MaterialDocumentImportJobMapper.ToDto)
            .ToArray();
        return Task.FromResult<IReadOnlyList<MaterialDocumentImportJobExceptionDto>?>(result);
    }
}
