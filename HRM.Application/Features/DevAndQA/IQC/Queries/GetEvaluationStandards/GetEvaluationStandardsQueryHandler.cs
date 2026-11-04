using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;

/// <summary>Điều phối use case GetEvaluationStandards qua service danh mục có scope và capability.</summary>
internal sealed class GetEvaluationStandardsQueryHandler(EvaluationStandardService service)
    : IRequestHandler<GetEvaluationStandardsQuery, OperationResult<PagedResult<EvaluationStandardDto>>>
{
    public Task<OperationResult<PagedResult<EvaluationStandardDto>>> Handle(GetEvaluationStandardsQuery request, CancellationToken ct) => service.ListAsync(request, ct);
}
