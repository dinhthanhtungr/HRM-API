using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Điều kiện lấy calendar CRM tổng hợp từ task, interaction và work plan.
/// </summary>
public sealed class CustomerCrmCalendarQuery
{
    public CustomerCrmCalendarView View { get; init; } = CustomerCrmCalendarView.Month;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSaleEmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public bool OnlyMine { get; init; }
    public IReadOnlyCollection<CustomerCrmCalendarSourceType>? ActivityTypes { get; init; }
    public bool ShowWeekends { get; init; } = true;
    public bool ShowCompletedTasks { get; init; } = true;
}

/// <summary>
/// Điều kiện tổng hợp activity header theo khách hàng.
/// </summary>
public sealed class CustomerCrmActivityHeaderQuery : PaginationQuery
{
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSaleEmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public bool OnlyMine { get; init; }
    public bool IncludeCompleted { get; init; } = true;
    public bool IncludeCanceled { get; init; }
    public IReadOnlyCollection<CustomerCrmCalendarSourceType>? ActivityTypes { get; init; }
}

/// <summary>
/// Điều kiện tạo báo cáo hoạt động và doanh số khách hàng theo kỳ.
/// </summary>
public sealed class CustomerActivityCalendarReportQuery : PaginationQuery
{
    public int? Year { get; init; }
    public int? Month { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSaleEmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public bool OnlyMine { get; init; }

    /// <summary>
    /// Bổ sung khách không có interaction trong kỳ nhưng có doanh số giao hàng trong kỳ.
    /// Khách không có cả interaction lẫn doanh số không được trả về.
    /// </summary>
    public bool IncludeCustomersWithoutActivity { get; init; }
}
