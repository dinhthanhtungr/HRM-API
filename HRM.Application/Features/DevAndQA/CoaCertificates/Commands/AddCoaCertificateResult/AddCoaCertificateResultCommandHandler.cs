using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Commands.AddCoaCertificateResult;

internal sealed class AddCoaCertificateResultCommandHandler(CoaCertificateService service) : IRequestHandler<AddCoaCertificateResultCommand, EvaluationResultDto>
{
    public Task<EvaluationResultDto> Handle(AddCoaCertificateResultCommand request, CancellationToken ct) => service.AddCoaResultAsync(request, ct);
}
