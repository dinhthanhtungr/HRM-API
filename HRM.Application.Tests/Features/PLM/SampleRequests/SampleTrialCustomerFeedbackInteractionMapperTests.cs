using HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleTrialCustomerFeedbackInteraction;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialCustomerFeedbackInteractionMapperTests
{
    [Fact]
    public void ToCrmRequest_MapsRouteContextAndFeedbackFields()
    {
        var customerId = Guid.NewGuid();
        var trialId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var interactionAt = new DateTime(2026, 8, 13, 9, 30, 0);
        var command = new RecordSampleTrialCustomerFeedbackInteractionCommand
        {
            SampleRequestId = Guid.NewGuid(),
            SampleRequestSampleTrialId = trialId,
            IdempotencyKey = idempotencyKey,
            InteractionType = CustomerInteractionType.Call,
            Content = "Khách đã nhận mẫu.",
            CustomerReplyStatus = "RECEIVED",
            CustomerReplyNote = "Khách đang kiểm tra.",
            InteractionAt = interactionAt,
            NextAction = "Gọi lại sau ba ngày"
        };

        var result = SampleTrialCustomerFeedbackInteractionMapper.ToCrmRequest(command, customerId);

        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(trialId, result.SampleRequestSampleTrialId);
        Assert.Equal(idempotencyKey, result.IdempotencyKey);
        Assert.Equal(CustomerInteractionType.Call, result.InteractionType);
        Assert.Equal(interactionAt, result.InteractionAt);
        Assert.Equal("RECEIVED", result.CustomerReplyStatus);
        Assert.Equal("Khách đang kiểm tra.", result.CustomerReplyNote);
        Assert.Equal("Gọi lại sau ba ngày", result.NextAction);
    }
}
