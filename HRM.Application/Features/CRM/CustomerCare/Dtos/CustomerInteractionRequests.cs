using HRM.Domain.Enums.CustomerEnum;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Dữ liệu ghi nhận một lần tương tác với khách hàng.
/// Nếu có NextFollowUpDate, API có thể tạo follow-up task liên kết với interaction này.
/// </summary>
public sealed class CreateCustomerInteractionRequest
{
    public Guid CustomerId { get; set; }
    public Guid? ContactId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionType InteractionType { get; set; } = CustomerInteractionType.Call;
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public DateTime InteractionAt { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
}

/// <summary>
/// Dữ liệu patch interaction; property null được hiểu là không thay đổi trường tương ứng.
/// </summary>
public sealed class UpdateCustomerInteractionRequest
{
    public Guid? ContactId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionType? InteractionType { get; set; }
    public string? Subject { get; set; }
    public string? Content { get; set; }
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public DateTime? InteractionAt { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public bool? IsActive { get; set; }
}
