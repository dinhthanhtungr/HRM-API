using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Quản lý follow-up task, assignee và các read model work plan CRM trên entity dùng chung thuộc WorkTaskSchema.
/// </summary>
public interface ICustomerCrmWorkService
{
    /// <summary>
    /// Lấy chi tiết follow-up task trong company và customer visibility scope hiện tại.
    /// </summary>
    Task<OperationResult<CustomerFollowUpTaskDto>> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách task được giao trực tiếp hoặc giao hỗ trợ cho nhân viên hiện tại.
    /// </summary>
    Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> GetMyTasksAsync(CustomerFollowUpTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy follow-up task của một khách hàng theo bộ lọc và phân trang.
    /// </summary>
    Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> GetCustomerTasksAsync(Guid customerId, CustomerFollowUpTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách assignee đang hoạt động của follow-up task.
    /// </summary>
    Task<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>> GetTaskAssigneesAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết work plan trong phạm vi người dùng được phép truy cập.
    /// </summary>
    Task<OperationResult<CustomerWorkPlanDto>> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách work plan của một khách hàng theo bộ lọc và phân trang.
    /// </summary>
    Task<OperationResult<PagedResult<CustomerWorkPlanDto>>> GetCustomerPlansAsync(Guid customerId, CustomerWorkPlanQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cung cấp các read model tổng hợp cho calendar, activity header, dashboard và báo cáo CRM.
/// </summary>
public interface ICustomerCrmAnalyticsService
{
    /// <summary>
    /// Hợp nhất task, interaction và work plan thành danh sách sự kiện calendar trong khoảng thời gian yêu cầu.
    /// </summary>
    Task<OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>> GetCalendarAsync(CustomerCrmCalendarQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổng hợp số task và interaction theo khách hàng để hiển thị danh sách activity header.
    /// </summary>
    Task<OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>> GetActivityHeadersAsync(CustomerCrmActivityHeaderQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy các chỉ số CRM cá nhân của sale hiện tại.
    /// </summary>
    Task<OperationResult<SaleCrmDashboardDto>> GetSaleDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy các chỉ số CRM tổng hợp trong visibility scope của leader.
    /// </summary>
    Task<OperationResult<TeamCrmDashboardDto>> GetLeaderDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy các chỉ số CRM tổng hợp dành cho director/admin.
    /// </summary>
    Task<OperationResult<TeamCrmDashboardDto>> GetDirectorDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổng hợp hoạt động, liên hệ, task và doanh số theo khách hàng trong kỳ báo cáo.
    /// </summary>
    Task<OperationResult<CustomerActivityCalendarReportDto>> GetActivityReportAsync(CustomerActivityCalendarReportQuery query, CancellationToken cancellationToken = default);
}
