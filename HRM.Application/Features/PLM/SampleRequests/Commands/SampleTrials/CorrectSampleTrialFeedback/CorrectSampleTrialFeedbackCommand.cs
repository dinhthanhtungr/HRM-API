using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CorrectSampleTrialFeedback;

/// <summary>Sale sửa phản hồi mới nhất; đổi kết quả chỉ trước khi Lab tiếp tục, có lý do và token công thức.</summary>
public sealed class CorrectSampleTrialFeedbackCommand : IRequest<OperationResult<Guid>>
{
    public Guid SampleRequestId { get; set; }
    public Guid TrialId { get; set; }
    public DateTime CustomerReplyDate { get; set; }
    public string? CustomerReplyNote { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public HRM.Domain.Enums.SampleRequests.SampleTrialStatus? Status { get; set; }
    public string? CorrectionReason { get; set; }
    public DateTime? ExpectedFormulaUpdatedDate { get; set; }
    public DateTime? ExpectedTrialUpdatedDate { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
}
