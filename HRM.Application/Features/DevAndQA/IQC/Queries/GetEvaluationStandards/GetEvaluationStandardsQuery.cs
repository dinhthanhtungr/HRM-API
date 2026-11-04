using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using MediatR;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;

/// <summary>Danh mục phân trang theo company, tìm mã/tên và trạng thái sử dụng.</summary>
public sealed class GetEvaluationStandardsQuery : PaginationQuery, IRequest<OperationResult<PagedResult<EvaluationStandardDto>>>
{
    public bool? IsActive { get; init; }
}
