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
    public IReadOnlyList<CustomerInteractionReferenceRequest> References { get; set; } = Array.Empty<CustomerInteractionReferenceRequest>();
}

public sealed class CustomerInteractionReferenceRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public bool IsPrimary { get; set; }
    public string? ReferenceCodeSnapshot { get; set; }
    public string? ReferenceNameSnapshot { get; set; }
}

/// <summary>
/// Ghi nhận tình hình mẫu từ lịch chăm sóc và đồng bộ phản hồi khách trên SampleRequestSampleTrial.
/// </summary>
public sealed class CreateSampleTrialInteractionRequest
{
    public Guid? IdempotencyKey { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SampleRequestSampleTrialId { get; set; }
    public Guid? ContactId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionType InteractionType { get; set; } = CustomerInteractionType.SampleTrial;
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public DateTime InteractionAt { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? CustomerReplyStatus { get; set; }
    public DateTime? CustomerReplyDate { get; set; }
    public string? CustomerReplyNote { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedTrialUpdatedDate { get; set; }
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
