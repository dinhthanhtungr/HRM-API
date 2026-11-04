using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.InitializeIqcEvaluation;

internal sealed class InitializeIqcEvaluationCommandHandler(IqcEvaluationService service) : IRequestHandler<InitializeIqcEvaluationCommand, IqcEvaluationDto>
{
    public Task<IqcEvaluationDto> Handle(InitializeIqcEvaluationCommand request, CancellationToken ct) => service.InitializeIqcAsync(request, ct);
}
