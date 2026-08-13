using HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class SampleTrialInteractionMutationTests
{
    [Fact]
    public void ApplyCustomerReply_UpdatesFeedbackAndAuditTogether()
    {
        var employeeId = Guid.NewGuid();
        var interactionAt = new DateTime(2026, 8, 13, 9, 30, 0);
        var savedAt = new DateTime(2026, 8, 13, 9, 31, 0);
        var trial = new SampleRequestSampleTrial();
        var request = new CreateSampleTrialInteractionRequest
        {
            Content = "Khách đang kiểm tra mẫu.",
            CustomerReplyStatus = " RECEIVED ",
            CustomerReplyNote = " Đã giao tận tay ",
            CustomerReplyDate = interactionAt
        };

        SampleTrialInteractionMutation.ApplyCustomerReply(
            trial,
            request,
            interactionAt,
            employeeId,
            savedAt);

        Assert.Equal("RECEIVED", trial.CustomerReplyStatus);
        Assert.Equal("Đã giao tận tay", trial.CustomerReplyNote);
        Assert.Equal(interactionAt, trial.CustomerReplyDate);
        Assert.Equal(employeeId, trial.CustomerReplyByEmployeeId);
        Assert.Equal(employeeId, trial.UpdatedBy);
        Assert.Equal(savedAt, trial.UpdatedDate);
    }
}
