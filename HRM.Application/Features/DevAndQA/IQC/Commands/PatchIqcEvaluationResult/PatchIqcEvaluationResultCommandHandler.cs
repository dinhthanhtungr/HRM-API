using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqcEvaluationResult;

internal sealed class PatchIqcEvaluationResultCommandHandler(IqcEvaluationService service) : IRequestHandler<PatchIqcEvaluationResultCommand, EvaluationResultDto>
{
    public Task<EvaluationResultDto> Handle(PatchIqcEvaluationResultCommand request, CancellationToken ct) => service.PatchIqcResultAsync(request, ct);
}
