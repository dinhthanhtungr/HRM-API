using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkflowPublicationRulesTests
{
    [Theory]
    [InlineData("CustomerFeedbackRecorded", false)]
    [InlineData("LabStandardReceived", false)]
    [InlineData("CustomerPriceAccepted", false)]
    [InlineData("CustomerPriceAcceptanceRevoked", false)]
    [InlineData("SampleRequestWorkflowCreated", true)]
    [InlineData("LabFormulaConfirmed", true)]
    [InlineData("SampleSent", true)]
    [InlineData("SampleRequestCancelled", true)]
    [InlineData("QuotationFormulaFinalized", false)]
    [InlineData("CustomerPriceFeedbackShared", false)]
    public void LegacyKeepsOperationalNoticesButSuppressesTrackingNotices(string eventType, bool expected)
    {
        Assert.Equal(expected, SampleRequestWorkflowPublicationRules.ShouldDispatch(new DateTime(2026, 10, 31), eventType));
        Assert.True(SampleRequestWorkflowPublicationRules.ShouldDispatch(new DateTime(2026, 11, 1), eventType));
    }

    [Theory]
    [InlineData("SampleRequestWorkflowCreated")]
    [InlineData("LabStandardReceived")]
    [InlineData("LabFormulaConfirmed")]
    [InlineData("CustomerPriceAccepted")]
    [InlineData("CustomerPriceAcceptanceRevoked")]
    [InlineData("SampleSent")]
    [InlineData("CustomerFeedbackRecorded")]
    [InlineData("SampleRequestCancelled")]
    [InlineData("QuotationFormulaFinalized")]
    [InlineData("CustomerPriceFeedbackShared")]
    public void PersistedBusinessActionMayPublishWithoutGrantingChatAccess(string eventType)
    {
        var evt = Event(eventType);
        Assert.True(SampleRequestWorkflowPublicationRules.IsTrustedPublication(evt,
            evt.SampleRequestId, evt.CompanyId, evt.RecordedByEmployeeId!.Value, true));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("company")]
    [InlineData("request")]
    [InlineData("inactive")]
    [InlineData("unknown-event")]
    [InlineData("missing-event")]
    public void MissingOrMismatchedProvenanceCannotBypassNormalConversationPolicy(string mismatch)
    {
        var evt = Event("LabFormulaConfirmed");
        if (mismatch == "unknown-event") evt.EventType = "ArbitraryChatMessage";
        Assert.False(SampleRequestWorkflowPublicationRules.IsTrustedPublication(
            mismatch == "missing-event" ? null : evt,
            mismatch == "request" ? Guid.NewGuid() : evt.SampleRequestId,
            mismatch == "company" ? Guid.NewGuid() : evt.CompanyId,
            mismatch == "actor" ? Guid.NewGuid() : evt.RecordedByEmployeeId!.Value,
            mismatch != "inactive"));
    }

    private static SampleRequestWorkflowEvent Event(string eventType) => new()
    {
        CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(),
        RecordedByEmployeeId = Guid.NewGuid(), EventType = eventType
    };
}
