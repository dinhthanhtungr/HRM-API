using HRM.Domain.Enums.WorkTaskEnums;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Dữ liệu tạo kế hoạch chăm sóc khách hàng dài hạn trên WorkPlanSchema.
/// </summary>
public sealed class CreateCustomerWorkPlanRequest
{
    public Guid CustomerId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Strategy { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkPlanStatus Status { get; set; } = WorkPlanStatus.Active;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Normal;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
}

/// <summary>
/// Dữ liệu patch kế hoạch chăm sóc khách hàng; property null được hiểu là không thay đổi.
/// </summary>
public sealed class UpdateCustomerWorkPlanRequest
{
    public string? PlanName { get; set; }
    public string? Objective { get; set; }
    public string? Strategy { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? NextAction { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkPlanStatus? Status { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public bool? IsActive { get; set; }
}
