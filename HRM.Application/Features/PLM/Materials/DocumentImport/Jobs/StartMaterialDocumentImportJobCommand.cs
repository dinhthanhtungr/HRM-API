using HRM.Application.Abstractions.Security;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public sealed record StartMaterialDocumentImportJobCommand
    : IRequest<StartMaterialDocumentImportJobResult?>;

public sealed record StartMaterialDocumentImportJobResult(
    bool Accepted,
    MaterialDocumentImportJobDto Job);

internal sealed class StartMaterialDocumentImportJobCommandHandler
    : IRequestHandler<StartMaterialDocumentImportJobCommand, StartMaterialDocumentImportJobResult?>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialDocumentImportJobQueue _jobQueue;

    public StartMaterialDocumentImportJobCommandHandler(
        ICurrentUser currentUser,
        IMaterialDocumentImportJobQueue jobQueue)
    {
        _currentUser = currentUser;
        _jobQueue = jobQueue;
    }

    public Task<StartMaterialDocumentImportJobResult?> Handle(
        StartMaterialDocumentImportJobCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
        {
            return Task.FromResult<StartMaterialDocumentImportJobResult?>(null);
        }

        var accepted = _jobQueue.TryEnqueue(
            companyId,
            _currentUser.UserId,
            _currentUser.EmployeeId,
            out var job);

        return Task.FromResult<StartMaterialDocumentImportJobResult?>(
            new StartMaterialDocumentImportJobResult(
                accepted,
                MaterialDocumentImportJobMapper.ToDto(job)));
    }
}
