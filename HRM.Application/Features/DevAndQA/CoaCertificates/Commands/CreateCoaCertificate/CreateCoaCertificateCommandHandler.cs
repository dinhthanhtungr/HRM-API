using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CreateCoaCertificate;

internal sealed class CreateCoaCertificateCommandHandler(CoaCertificateService service) : IRequestHandler<CreateCoaCertificateCommand, CoaCertificateDto>
{
    public Task<CoaCertificateDto> Handle(CreateCoaCertificateCommand request, CancellationToken ct) => service.CreateCoaAsync(request, ct);
}
