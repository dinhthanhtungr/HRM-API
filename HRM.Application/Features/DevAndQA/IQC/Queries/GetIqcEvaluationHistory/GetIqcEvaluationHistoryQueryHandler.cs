using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;

internal sealed class GetIqcEvaluationHistoryQueryHandler(IqcEvaluationHistoryReader service)
    : IRequestHandler<GetIqcEvaluationHistoryQuery, PagedResult<IqcEvaluationHistoryDto>>
{
    public Task<PagedResult<IqcEvaluationHistoryDto>> Handle(GetIqcEvaluationHistoryQuery request, CancellationToken ct)
        => service.GetIqcEvaluationHistoryAsync(request, ct);
}
