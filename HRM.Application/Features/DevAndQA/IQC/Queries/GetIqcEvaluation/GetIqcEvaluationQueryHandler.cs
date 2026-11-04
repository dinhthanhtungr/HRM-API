using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluation;

internal sealed class GetIqcEvaluationQueryHandler(IqcEvaluationService service) : IRequestHandler<GetIqcEvaluationQuery, IqcEvaluationDto>
{
    public Task<IqcEvaluationDto> Handle(GetIqcEvaluationQuery request, CancellationToken ct) => service.GetIqcEvaluationAsync(request.VoucherDetailId, ct);
}
