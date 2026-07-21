using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Chi tiết một lần tương tác với khách hàng.
/// </summary>
public sealed class CustomerInteractionDto
{
    public Guid InteractionId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? ContactId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionType InteractionType { get; set; }
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public DateTime InteractionAt { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Chi tiết follow-up task CRM được ánh xạ từ WorkTask và các reference liên quan.
/// </summary>
public sealed class CustomerFollowUpTaskDto
{
    public Guid WorkTaskId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CustomerInteractionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus Status { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CompletionNote { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<CustomerFollowUpTaskAssigneeDto> Assignees { get; set; } = Array.Empty<CustomerFollowUpTaskAssigneeDto>();
}

/// <summary>
/// Nhân viên đang được gán vào follow-up task.
/// </summary>
public sealed class CustomerFollowUpTaskAssigneeDto
{
    public Guid AssigneeId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeExternalId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Chi tiết kế hoạch chăm sóc khách hàng được ánh xạ từ WorkPlan.
/// </summary>
public sealed class CustomerWorkPlanDto
{
    public Guid WorkPlanId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Strategy { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkPlanStatus Status { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Read model sự kiện calendar thống nhất cho task, interaction và work plan.
/// </summary>
public sealed class CustomerCrmCalendarEventDto
{
    public string Id { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerCrmCalendarSourceType SourceType { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventTypeColorKey ColorKey { get; set; }
}

/// <summary>
/// Các chỉ số hoạt động CRM tổng quan của một khách hàng.
/// </summary>
public sealed class CustomerCrmActivityHeaderDto
{
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int OpenTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int CanceledTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public int InteractionCount { get; set; }
    public DateTime? LastActivityDate { get; set; }
}

/// <summary>
/// Các chỉ số dashboard cá nhân của sale hiện tại.
/// </summary>
public sealed class SaleCrmDashboardDto
{
    public int TodayTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int PendingTasks { get; set; }
    public int CompletedTasksThisMonth { get; set; }
    public int InteractionsThisMonth { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

/// <summary>
/// Các chỉ số dashboard tổng hợp theo visibility scope của cấp quản lý.
/// </summary>
public sealed class TeamCrmDashboardDto
{
    public int ActiveCustomers { get; set; }
    public int OpenTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int CompletedTasksThisMonth { get; set; }
    public int InteractionsThisMonth { get; set; }
    public int ActiveWorkPlans { get; set; }
}

/// <summary>
/// Kết quả báo cáo hoạt động và doanh số khách hàng trong kỳ.
/// </summary>
public sealed class CustomerActivityCalendarReportDto
{
    public CustomerActivityReportHeaderDto Header { get; set; } = default!;
    public PagedResult<CustomerActivityReportRowDto> Customers { get; set; } = default!;
}

/// <summary>
/// Số liệu tổng hợp của toàn bộ tập khách hàng sau khi áp dụng kỳ báo cáo, visibility và filter.
/// Các giá trị không phụ thuộc vào trang customer đang được trả về.
/// </summary>
public sealed class CustomerActivityReportHeaderDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalRevenueAmount { get; set; }
    public int TotalMeetingVisitCount { get; set; }
    public int TotalOtherInteractionCount { get; set; }
}

/// <summary>
/// Một dòng thống kê hoạt động, task và doanh số của khách hàng.
/// </summary>
public sealed class CustomerActivityReportRowDto
{
    public Guid CustomerId { get; set; }
    public string RevenueGroupCode { get; set; } = string.Empty;
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public decimal RevenueAmount { get; set; }
    public int ActivityCount { get; set; }
    public int ContactCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int OpenTaskCount { get; set; }
    public IReadOnlyList<CustomerActivityReportDailyContactDto> DailyContacts { get; set; } = Array.Empty<CustomerActivityReportDailyContactDto>();
}

/// <summary>
/// Dữ liệu theo ngày để FE vẽ ô liên hệ trong kỳ báo cáo, level 0-4 dùng cho thang màu kiểu contribution grid.
/// </summary>
public sealed class CustomerActivityReportDailyContactDto
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public int InteractionCount { get; set; }
    public int MeetingVisitCount { get; set; }
    public int OtherInteractionCount { get; set; }
    public int Level { get; set; }
}
