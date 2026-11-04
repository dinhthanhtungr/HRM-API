using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.IssueCoaCertificate;

internal sealed class IssueCoaCertificateCommandHandler(CoaCertificateService service) : IRequestHandler<IssueCoaCertificateCommand, CoaCertificateDto>
{
    public Task<CoaCertificateDto> Handle(IssueCoaCertificateCommand request, CancellationToken ct) => service.IssueCoaAsync(request.Id, ct);
}
