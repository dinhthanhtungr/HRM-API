using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleBatchFeedbackMessageTests
{
    [Fact]
    public void GroupedPublicationRequiresScopedActorAndNeverUsesTheRetryHashAsMessageText()
    {
        var evt = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(),
            RecordedByEmployeeId = Guid.NewGuid(), EventType = RecordSampleBatchFeedbackCommandHandler.EventType, Note = "retry-hash" };
        Assert.True(SampleRequestWorkflowPublicationRules.IsTrustedPublication(evt, evt.SampleRequestId, evt.CompanyId, evt.RecordedByEmployeeId.Value, true));
        Assert.False(SampleRequestWorkflowPublicationRules.IsTrustedPublication(evt, evt.SampleRequestId, Guid.NewGuid(), evt.RecordedByEmployeeId.Value, true));
        var message = WorkflowMessageText.Build(evt, null, "Feedback", null, null, false, "Combined notes", null);
        Assert.Equal("Combined notes", message.Body);
        var fallback = WorkflowMessageText.Build(evt, null, "Feedback", null, null, false, null, null);
        Assert.DoesNotContain("retry-hash", fallback.Body);
    }
    [Fact]
    public void IncludesOnlyNonEmptyFeedbackAndKeepsOutcomeAndFormulaCode()
    {
        var message = SampleBatchFeedbackMessage.Build("TP_31155", new(2026, 11, 15), [
            ("VU1", true, "  Ok được á  "), ("VU2", false, "  "), ("VU3", false, "Màu chưa đúng")]);
        Assert.Contains("15/11/2026", message);
        Assert.Contains("VU1 · Đạt: Ok được á", message);
        Assert.Contains("VU3 · Không đạt: Màu chưa đúng", message);
        Assert.DoesNotContain("VU2", message);
        Assert.Null(SampleBatchFeedbackMessage.Build("TP", new(2026, 11, 15), [("VU", false, null), ("VU2", true, " ")]));
    }

    [Fact]
    public void LongNotesKeepAPreviewOfEverySampleWithinTheExistingMessageLimit()
    {
        var rows = Enumerable.Range(1, 30).Select(i => ((string?)$"VU{i:00}", true, (string?)new string('x', 5000)));
        var message = SampleBatchFeedbackMessage.Build(new string('T', 100), new(2026, 11, 15), rows)!;
        Assert.InRange(message.Length, 1, 2000);
        foreach (var i in Enumerable.Range(1, 30)) Assert.Contains($"VU{i:00} · Đạt:", message);
    }
}
