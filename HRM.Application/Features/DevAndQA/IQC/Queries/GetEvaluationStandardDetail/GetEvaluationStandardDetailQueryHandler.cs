using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;

/// <summary>Điều phối use case GetEvaluationStandardDetail qua service danh mục có scope và capability.</summary>
internal sealed class GetEvaluationStandardDetailQueryHandler(EvaluationStandardService service)
    : IRequestHandler<GetEvaluationStandardDetailQuery, OperationResult<EvaluationStandardDto?>>
{
    public Task<OperationResult<EvaluationStandardDto?>> Handle(GetEvaluationStandardDetailQuery request, CancellationToken ct) => service.DetailAsync(request, ct);
}
