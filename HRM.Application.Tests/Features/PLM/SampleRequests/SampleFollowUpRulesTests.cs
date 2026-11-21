using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleFollowUpRulesTests
{
    [Fact]
    public void ExplicitSamplePlanAllowsNewDeliveryWithoutRewritingOldPendingFeedback()
    {
        var at = new DateTime(2026, 11, 18);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            CreatedDate = at, Status = "InProgress", SampleQuantity = 2 };
        var old = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            IsActive = true, Status = SampleTrialStatus.WaitingCustomerFeedback, SentDate = at, CreatedDate = at,
            DeliveredSampleQuantityKg = 2 };
        Assert.Contains("RequestSample", SampleRequestPlanRules.Actions(request, old, null, true, false));
        Assert.Empty(SampleRequestPlanRules.Actions(request, old, null, false, false));
        Assert.True(ChipThenSampleRules.HasBlockingPending([old]));
        var plan = new SampleRequestWorkflowEvent { CompanyId = request.CompanyId, SampleRequestId = request.SampleRequestId,
            EventType = SampleFlowRules.KgRequested, RecordedAt = at.AddMinutes(1) };
        Assert.False(ChipThenSampleRules.HasBlockingPending([old], events: [plan]));
        var laterChip = new SampleRequestWorkflowEvent { CompanyId = request.CompanyId, SampleRequestId = request.SampleRequestId,
            EventType = SampleFlowRules.ChipRequested, RecordedAt = at.AddMinutes(3) };
        Assert.False(ChipThenSampleRules.HasBlockingPending([old], events: [laterChip, plan]));
        old.SentDate = null;
        Assert.True(ChipThenSampleRules.HasBlockingPending([old], events: [plan]));
        old.SentDate = at;
        var newSend = new SampleRequestWorkflowEvent { CompanyId = request.CompanyId, SampleRequestId = request.SampleRequestId,
            EventType = HRM.Application.Features.PLM.SampleRequests.Rules.SampleRequestWorkflowEvents.SampleSent,
            SampleRequestSampleTrialId = old.SampleRequestSampleTrialId, RecordedAt = at.AddMinutes(2) };
        Assert.True(ChipThenSampleRules.HasBlockingPending([old], events: [newSend, plan]));
        Assert.Equal(SampleTrialStatus.WaitingCustomerFeedback, old.Status);
        Assert.Null(old.CustomerReplyDate);
    }

    [Theory]
    [InlineData(2024, 10, 31)]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 11, 1)]
    [InlineData(2026, 11, 2)]
    public void DatesAndClosedStatusDoNotRemoveAuthorizedFollowUpChoices(int year, int month, int day)
    {
        foreach (var status in new[] { "New", "InProgress", "Completed", "Cancelled" })
        {
            var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
                CreatedDate = new(year, month, day), Status = status };
            var actions = SampleRequestPlanRules.Actions(request, null, null, true, false);
            Assert.Contains("RequestSample", actions);
            Assert.Contains("RequestChip", actions);
            Assert.Contains(ChipThenSampleRules.Action, actions);
            Assert.Empty(SampleRequestPlanRules.Actions(request, null, null, false, false));
            var marker = new SampleRequestWorkflowEvent { SampleRequestId = request.SampleRequestId,
                CompanyId = request.CompanyId, EventType = SampleFollowUpRules.Started };
            Assert.False(SampleFollowUpRules.IsLegacy(request, [marker]));
            marker.CompanyId = Guid.NewGuid();
            Assert.Equal(year < 2026 || month < 11, SampleFollowUpRules.IsLegacy(request, [marker]));
            Assert.Equal(new DateTime(year, month, day), request.CreatedDate);
        }
    }

    [Fact]
    public void OldRequestOnlyUsesChipThenKgAfterExplicitActivation()
    {
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            CreatedDate = new(2024, 1, 1), Status = "InProgress", SampleQuantity = 2, InfoType = "Thẻ màu (1)" };
        var formula = new Formula { FormulaId = Guid.NewGuid(), Status = "Approved" };
        var plan = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.ChipThenSampleRequested,
            RecordedAt = new(2026, 11, 18), SampleRequestId = request.SampleRequestId, CompanyId = request.CompanyId };
        Assert.Equal("Legacy", SampleFlowStateReader.Build(request, null, formula, [plan], true, false).WorkflowMode);
        var marker = new SampleRequestWorkflowEvent { EventType = SampleFollowUpRules.Started,
            RecordedAt = plan.RecordedAt, SampleRequestId = request.SampleRequestId, CompanyId = request.CompanyId };
        var events = new[] { plan, marker };
        var before = SampleFlowStateReader.Build(request, null, formula, events, true, false);
        Assert.Equal("ChipAndSample", before.WorkflowMode);
        Assert.Equal("TrialSample", before.DeliveryKind);
        Assert.Contains("Send", before.AllowedActions);
        var chip = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId, IsActive = true,
            DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.WaitingCustomerFeedback,
            FormulaId = formula.FormulaId, CreatedDate = plan.RecordedAt.AddMinutes(1), SentDate = plan.RecordedAt.AddMinutes(1) };
        formula.Status = "SampleSent";
        var after = SampleFlowStateReader.Build(request, chip, formula, events, true, false, trials: [chip]);
        Assert.Equal("TrialSample", after.DeliveryKind);
        Assert.Contains("Send", after.AllowedActions);
        Assert.True(ChipThenSampleRules.HasBlockingPending([chip]));
        Assert.False(ChipThenSampleRules.HasBlockingPending([chip], true));
        chip.DeliveredSampleQuantityKg = null;
        Assert.True(ChipThenSampleRules.HasBlockingPending([chip], true));
        chip.DeliveredSampleQuantityKg = 2;
        Assert.True(ChipThenSampleRules.HasBlockingPending([chip], true));
    }
}
