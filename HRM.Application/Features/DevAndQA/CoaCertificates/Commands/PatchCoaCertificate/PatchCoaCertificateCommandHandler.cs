using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;

internal sealed class PatchCoaCertificateCommandHandler(CoaCertificateService service) : IRequestHandler<PatchCoaCertificateCommand, CoaCertificateDto>
{
    public Task<CoaCertificateDto> Handle(PatchCoaCertificateCommand request, CancellationToken ct) => service.PatchCoaAsync(request, ct);
}
