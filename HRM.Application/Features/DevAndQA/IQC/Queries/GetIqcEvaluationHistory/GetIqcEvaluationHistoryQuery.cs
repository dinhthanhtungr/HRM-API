using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;

/// <summary>Lịch sử phiếu QC cùng mã mặt hàng trong company, phân trang theo phiếu, không theo chỉ tiêu.</summary>
public sealed record GetIqcEvaluationHistoryQuery(long VoucherDetailId, int PageNumber = 1, int PageSize = 3)
    : IRequest<PagedResult<IqcEvaluationHistoryDto>>;
