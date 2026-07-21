using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.CustomerCrmAnalytics;

/// <summary>
/// Lấy dữ liệu calendar CRM tổng hợp từ task, interaction và work plan.
/// </summary>
public sealed class GetCustomerCrmCalendarQuery
    : IRequest<OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>>
{
    public CustomerCrmCalendarQuery Query { get; init; } = new();
}

internal sealed class GetCustomerCrmCalendarQueryHandler
    : IRequestHandler<GetCustomerCrmCalendarQuery, OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetCustomerCrmCalendarQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>> Handle(
        GetCustomerCrmCalendarQuery request,
        CancellationToken cancellationToken)
        => _analyticsService.GetCalendarAsync(request.Query, cancellationToken);
}

/// <summary>
/// Lấy dữ liệu tổng quan hoạt động CRM theo từng khách hàng.
/// </summary>
public sealed class GetCustomerCrmActivityHeadersQuery
    : IRequest<OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>>
{
    public CustomerCrmActivityHeaderQuery Query { get; init; } = new();
}

internal sealed class GetCustomerCrmActivityHeadersQueryHandler
    : IRequestHandler<GetCustomerCrmActivityHeadersQuery, OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetCustomerCrmActivityHeadersQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>> Handle(
        GetCustomerCrmActivityHeadersQuery request,
        CancellationToken cancellationToken)
        => _analyticsService.GetActivityHeadersAsync(request.Query, cancellationToken);
}

/// <summary>
/// Lấy dashboard cá nhân của sale đang đăng nhập.
/// </summary>
public sealed record GetSaleCrmDashboardQuery : IRequest<OperationResult<SaleCrmDashboardDto>>;

internal sealed class GetSaleCrmDashboardQueryHandler
    : IRequestHandler<GetSaleCrmDashboardQuery, OperationResult<SaleCrmDashboardDto>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetSaleCrmDashboardQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<SaleCrmDashboardDto>> Handle(GetSaleCrmDashboardQuery request, CancellationToken cancellationToken)
        => _analyticsService.GetSaleDashboardAsync(cancellationToken);
}

/// <summary>
/// Lấy dashboard trong visibility scope của leader.
/// </summary>
public sealed record GetLeaderCrmDashboardQuery : IRequest<OperationResult<TeamCrmDashboardDto>>;

internal sealed class GetLeaderCrmDashboardQueryHandler
    : IRequestHandler<GetLeaderCrmDashboardQuery, OperationResult<TeamCrmDashboardDto>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetLeaderCrmDashboardQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<TeamCrmDashboardDto>> Handle(GetLeaderCrmDashboardQuery request, CancellationToken cancellationToken)
        => _analyticsService.GetLeaderDashboardAsync(cancellationToken);
}

/// <summary>
/// Lấy dashboard tổng quan dành cho director/admin.
/// </summary>
public sealed record GetDirectorCrmDashboardQuery : IRequest<OperationResult<TeamCrmDashboardDto>>;

internal sealed class GetDirectorCrmDashboardQueryHandler
    : IRequestHandler<GetDirectorCrmDashboardQuery, OperationResult<TeamCrmDashboardDto>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetDirectorCrmDashboardQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<TeamCrmDashboardDto>> Handle(GetDirectorCrmDashboardQuery request, CancellationToken cancellationToken)
        => _analyticsService.GetDirectorDashboardAsync(cancellationToken);
}

/// <summary>
/// Tổng hợp hoạt động, liên hệ, task và doanh số khách hàng trong kỳ báo cáo.
/// </summary>
public sealed class GetCustomerActivityCalendarReportQuery
    : IRequest<OperationResult<CustomerActivityCalendarReportDto>>
{
    public CustomerActivityCalendarReportQuery Query { get; init; } = new();
}

internal sealed class GetCustomerActivityCalendarReportQueryHandler
    : IRequestHandler<GetCustomerActivityCalendarReportQuery, OperationResult<CustomerActivityCalendarReportDto>>
{
    private readonly ICustomerCrmAnalyticsService _analyticsService;

    public GetCustomerActivityCalendarReportQueryHandler(ICustomerCrmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<OperationResult<CustomerActivityCalendarReportDto>> Handle(
        GetCustomerActivityCalendarReportQuery request,
        CancellationToken cancellationToken)
        => _analyticsService.GetActivityReportAsync(request.Query, cancellationToken);
}
