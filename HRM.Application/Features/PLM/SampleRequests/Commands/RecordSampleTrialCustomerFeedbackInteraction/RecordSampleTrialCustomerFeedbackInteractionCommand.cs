using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleTrialCustomerFeedbackInteraction;

/// <summary>
/// Ghi nhận phản hồi khách hàng trên Trial và tạo CRM interaction trong cùng một lần lưu.
/// </summary>
public sealed class RecordSampleTrialCustomerFeedbackInteractionCommand
    : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    [JsonIgnore]
    public Guid SampleRequestSampleTrialId { get; set; }

    public Guid IdempotencyKey { get; set; }
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
