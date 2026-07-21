using HRM.Domain.Enums.WorkTaskEnums;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Dữ liệu tạo follow-up task CRM liên kết với một khách hàng và interaction tùy chọn.
/// </summary>
public sealed class CreateCustomerFollowUpTaskRequest
{
    public Guid CustomerId { get; set; }
    public Guid? CustomerInteractionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Normal;
    public DateTime? DueDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
}

/// <summary>
/// Dữ liệu patch follow-up task được lưu trên WorkTaskSchema.
/// </summary>
public sealed class UpdateCustomerFollowUpTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus? Status { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Dữ liệu đóng follow-up task với trạng thái kết thúc và ghi chú hoàn thành.
/// </summary>
public sealed class CompleteCustomerFollowUpTaskRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Done;
    public string? CompletionNote { get; set; }
}

/// <summary>
/// Dữ liệu gán một nhân viên hỗ trợ hoặc phụ trách chính cho follow-up task.
/// </summary>
public sealed class AddCustomerFollowUpTaskAssigneeRequest
{
    public Guid EmployeeId { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Dữ liệu gán các thành viên của một group vào follow-up task.
/// </summary>
public sealed class AddCustomerFollowUpTaskGroupAssigneesRequest
{
    public Guid GroupId { get; set; }
    public Guid? PrimaryEmployeeId { get; set; }
}
