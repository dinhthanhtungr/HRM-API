using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.CustomerWorkPlans;

/// <summary>
/// Lấy chi tiết work plan trong phạm vi truy cập hiện tại.
/// </summary>
public sealed record GetCustomerWorkPlanQuery(Guid PlanId)
    : IRequest<OperationResult<CustomerWorkPlanDto>>;

internal sealed class GetCustomerWorkPlanQueryHandler
    : IRequestHandler<GetCustomerWorkPlanQuery, OperationResult<CustomerWorkPlanDto>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetCustomerWorkPlanQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<CustomerWorkPlanDto>> Handle(GetCustomerWorkPlanQuery request, CancellationToken cancellationToken)
        => _workService.GetPlanAsync(request.PlanId, cancellationToken);
}

/// <summary>
/// Lấy danh sách work plan của một customer theo bộ lọc và phân trang.
/// </summary>
public sealed class GetCustomerWorkPlansByCustomerQuery
    : IRequest<OperationResult<PagedResult<CustomerWorkPlanDto>>>
{
    public Guid CustomerId { get; init; }
    public CustomerWorkPlanQuery Query { get; init; } = new();
}

internal sealed class GetCustomerWorkPlansByCustomerQueryHandler
    : IRequestHandler<GetCustomerWorkPlansByCustomerQuery, OperationResult<PagedResult<CustomerWorkPlanDto>>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetCustomerWorkPlansByCustomerQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<PagedResult<CustomerWorkPlanDto>>> Handle(
        GetCustomerWorkPlansByCustomerQuery request,
        CancellationToken cancellationToken)
        => _workService.GetCustomerPlansAsync(request.CustomerId, request.Query, cancellationToken);
}
