using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;

internal sealed class PatchCoaCertificateResultCommandHandler(CoaCertificateService service) : IRequestHandler<PatchCoaCertificateResultCommand, EvaluationResultDto>
{
    public Task<EvaluationResultDto> Handle(PatchCoaCertificateResultCommand request, CancellationToken ct) => service.PatchCoaResultAsync(request, ct);
}
