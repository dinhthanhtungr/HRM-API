using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CorrectSampleTrialFeedback;

/// <summary>Sale phụ trách sửa ghi chú/ngày phản hồi lần mới nhất, không đổi kết quả hoặc trạng thái.</summary>
public sealed class CorrectSampleTrialFeedbackCommand : IRequest<OperationResult<Guid>>
{
    public Guid SampleRequestId { get; set; }
    public Guid TrialId { get; set; }
    public DateTime CustomerReplyDate { get; set; }
    public string? CustomerReplyNote { get; set; }
    public DateTime? ExpectedTrialUpdatedDate { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
}
