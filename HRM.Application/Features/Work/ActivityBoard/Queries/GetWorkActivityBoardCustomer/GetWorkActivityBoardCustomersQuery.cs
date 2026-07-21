using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Work.ActivityBoard.Dtos;
using MediatR;

namespace HRM.Application.Features.Work.ActivityBoard.Queries.GetWorkActivityBoardCustomer;

/// <summary>
/// Lấy danh sách khách hàng có activity summary cho panel trái của work activity board.
/// Chi tiết task, plan và interaction được lấy bằng các API CRM phân trang theo customer được chọn.
/// </summary>
public sealed class GetWorkActivityBoardCustomersQuery
    : IRequest<OperationResult<PagedResult<WorkActivityBoardCustomerSummaryDto>>>
{
    public WorkActivityBoardCustomerListQueryDto Request { get; init; } = new();
}
