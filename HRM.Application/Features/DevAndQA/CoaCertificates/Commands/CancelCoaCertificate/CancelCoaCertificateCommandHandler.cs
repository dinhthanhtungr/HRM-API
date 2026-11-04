using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CancelCoaCertificate;

internal sealed class CancelCoaCertificateCommandHandler(CoaCertificateService service) : IRequestHandler<CancelCoaCertificateCommand, CoaCertificateDto>
{
    public Task<CoaCertificateDto> Handle(CancelCoaCertificateCommand request, CancellationToken ct) => service.CancelCoaAsync(request.Id, ct);
}
