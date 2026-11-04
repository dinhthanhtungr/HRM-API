using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmSampleRequestSampleReceipt;

/// <summary>
/// Sale xác nhận đã nhận mẫu từ action trên message; ngày nhận mặc định là thời điểm backend xử lý.
/// </summary>
public sealed class ConfirmSampleRequestSampleReceiptCommand
    : IRequest<OperationResult<SampleReceiptConfirmationDto>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    [JsonIgnore]
    public Guid SampleRequestSampleTrialId { get; set; }

    public Guid MessageId { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
}
