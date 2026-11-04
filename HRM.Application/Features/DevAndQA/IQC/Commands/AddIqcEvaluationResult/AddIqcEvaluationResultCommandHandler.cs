using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.AddIqcEvaluationResult;

internal sealed class AddIqcEvaluationResultCommandHandler(IqcEvaluationService service) : IRequestHandler<AddIqcEvaluationResultCommand, EvaluationResultDto>
{
    public Task<EvaluationResultDto> Handle(AddIqcEvaluationResultCommand request, CancellationToken ct) => service.AddIqcResultAsync(request, ct);
}
