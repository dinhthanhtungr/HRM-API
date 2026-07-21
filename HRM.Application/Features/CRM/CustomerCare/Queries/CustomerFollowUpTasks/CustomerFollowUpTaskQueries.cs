using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.CustomerFollowUpTasks;

/// <summary>
/// Lấy chi tiết follow-up task trong company và customer visibility scope hiện tại.
/// </summary>
public sealed record GetCustomerFollowUpTaskQuery(Guid TaskId)
    : IRequest<OperationResult<CustomerFollowUpTaskDto>>;

internal sealed class GetCustomerFollowUpTaskQueryHandler
    : IRequestHandler<GetCustomerFollowUpTaskQuery, OperationResult<CustomerFollowUpTaskDto>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetCustomerFollowUpTaskQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<CustomerFollowUpTaskDto>> Handle(GetCustomerFollowUpTaskQuery request, CancellationToken cancellationToken)
        => _workService.GetTaskAsync(request.TaskId, cancellationToken);
}

/// <summary>
/// Lấy các follow-up task được giao trực tiếp hoặc giao hỗ trợ cho nhân viên hiện tại.
/// </summary>
public sealed class GetMyCustomerFollowUpTasksQuery
    : IRequest<OperationResult<PagedResult<CustomerFollowUpTaskDto>>>
{
    public CustomerFollowUpTaskQuery Query { get; init; } = new();
}

internal sealed class GetMyCustomerFollowUpTasksQueryHandler
    : IRequestHandler<GetMyCustomerFollowUpTasksQuery, OperationResult<PagedResult<CustomerFollowUpTaskDto>>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetMyCustomerFollowUpTasksQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> Handle(
        GetMyCustomerFollowUpTasksQuery request,
        CancellationToken cancellationToken)
        => _workService.GetMyTasksAsync(request.Query, cancellationToken);
}

/// <summary>
/// Lấy follow-up task của một customer theo bộ lọc và phân trang.
/// </summary>
public sealed class GetCustomerFollowUpTasksByCustomerQuery
    : IRequest<OperationResult<PagedResult<CustomerFollowUpTaskDto>>>
{
    public Guid CustomerId { get; init; }
    public CustomerFollowUpTaskQuery Query { get; init; } = new();
}

internal sealed class GetCustomerFollowUpTasksByCustomerQueryHandler
    : IRequestHandler<GetCustomerFollowUpTasksByCustomerQuery, OperationResult<PagedResult<CustomerFollowUpTaskDto>>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetCustomerFollowUpTasksByCustomerQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> Handle(
        GetCustomerFollowUpTasksByCustomerQuery request,
        CancellationToken cancellationToken)
        => _workService.GetCustomerTasksAsync(request.CustomerId, request.Query, cancellationToken);
}

/// <summary>
/// Lấy danh sách assignee đang hoạt động của follow-up task.
/// </summary>
public sealed record GetCustomerFollowUpTaskAssigneesQuery(Guid TaskId)
    : IRequest<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>>;

internal sealed class GetCustomerFollowUpTaskAssigneesQueryHandler
    : IRequestHandler<GetCustomerFollowUpTaskAssigneesQuery, OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>>
{
    private readonly ICustomerCrmWorkService _workService;

    public GetCustomerFollowUpTaskAssigneesQueryHandler(ICustomerCrmWorkService workService)
    {
        _workService = workService;
    }

    public Task<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>> Handle(
        GetCustomerFollowUpTaskAssigneesQuery request,
        CancellationToken cancellationToken)
        => _workService.GetTaskAssigneesAsync(request.TaskId, cancellationToken);
}
