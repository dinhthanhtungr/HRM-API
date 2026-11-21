using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ChipThenSampleTests
{
    [Fact]
    public void KgContinuationDoesNotInventChipFeedbackAndPendingKgStillBlocksAnotherSend()
    {
        var formula = Formula("SampleSent");
        var chip = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback); chip.TrialNo = 1;
        var state = SampleFlowStateReader.Build(request, chip, formula, [Plan()], true, true, trials: [chip]);
        Assert.Equal("TrialSample", state.DeliveryKind); Assert.Contains("Send", state.AllowedActions);
        Assert.Equal("ReadyToSendSample", state.Stage);
        Assert.Null(chip.CustomerReplyDate); Assert.Null(chip.CustomerReplyNote);
        Assert.True(ChipThenSampleRules.HasBlockingPending([chip]));
        Assert.False(ChipThenSampleRules.HasBlockingPending([chip], true));
        var kg = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId, FormulaId = formula.FormulaId,
            TrialNo = 2, IsActive = true, DeliveredSampleQuantityKg = 100, SentDate = Start.AddHours(2), Status = SampleTrialStatus.WaitingCustomerFeedback };
        Assert.True(ChipThenSampleRules.HasBlockingPending([chip, kg], true));
        kg.Status = SampleTrialStatus.Approved;
        Assert.False(ChipThenSampleRules.HasBlockingPending([chip, kg]));
        Assert.True(KgSampleCompletionRules.IsSatisfied(kg, formula.FormulaId, [Plan()], request));
        request.InfoType = "Quotation";
        Assert.False(KgSampleCompletionRules.IsSatisfied(kg, formula.FormulaId, [Plan()], request));
        Assert.True(KgSampleCompletionRules.IsSatisfied(kg, formula.FormulaId,
            [new() { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = formula.FormulaId }, Plan()], request));
        request.InfoType = "Thẻ màu (2)";
        Assert.False(KgSampleCompletionRules.IsSatisfied(kg, formula.FormulaId,
            [new() { EventType = SampleFlowRules.QuotationRequired }, Plan()], request));
    }
    [Fact]
    public void CombinedPlanUsesTheExistingScopedLabAudienceAndTrustedPublication()
    {
        var evt = Plan();
        var company = Guid.NewGuid(); var actor = Guid.NewGuid();
        evt.CompanyId = company; evt.RecordedByEmployeeId = actor;
        Assert.True(SampleRequestWorkflowPublicationRules.IsTrustedPublication(evt, request.SampleRequestId, company, actor, true));
        Assert.Equal(SampleRequestNotificationAudience.LabLeaders | SampleRequestNotificationAudience.LabAdmins,
            SampleRequestNotificationAudienceRules.For(TopicNotifications.SampleRequestMessageCreated, evt.EventType));
    }
    private static readonly DateTime Start = new(2026, 11, 10, 8, 0, 0);
    private readonly SampleRequest request = new() { SampleRequestId = Guid.NewGuid(), CreatedDate = Start,
        Status = "InProgress", SampleQuantity = 100, InfoType = "Thẻ màu (2)", IsActive = true };

    private Formula Formula(string status = "Approved") => new() { FormulaId = Guid.NewGuid(), Status = status, IsActive = true };
    private SampleRequestWorkflowEvent Plan(string type = SampleFlowRules.ChipThenSampleRequested) => new()
        { EventType = type, RecordedAt = Start, SampleRequestId = request.SampleRequestId };
    private SampleRequestSampleTrial Chip(Formula formula, SampleTrialStatus status) => new()
    {
        SampleRequestId = request.SampleRequestId, FormulaId = formula.FormulaId, Formula = formula,
        SampleRequestSampleTrialId = Guid.NewGuid(), IsActive = true, DeliveredSampleQuantityKg = 0,
        Status = status, CreatedDate = Start.AddHours(1), SentDate = Start.AddHours(1)
    };

    [Fact]
    public void CombinedPlanShowsOneKgSendEvenWithoutPriceAcceptance()
    {
        var state = SampleFlowStateReader.Build(request, null, Formula(), [Plan()], true, false);
        Assert.Equal("ReadyToSendSample", state.Stage);
        Assert.Equal("TrialSample", state.DeliveryKind);
        Assert.Contains("Send", state.AllowedActions);
        Assert.Equal(100, state.RequestedKg);
    }

    [Fact]
    public void CombinedPlanCannotReuseAFormulaAlreadySentInKg()
    {
        var state = SampleFlowStateReader.Build(request, null, Formula(), [Plan()], true, false, hasSentKg: true);
        Assert.Equal("PrepareSample", state.Stage);
        Assert.DoesNotContain("Send", state.AllowedActions);
        Assert.False(state.DeliveryNeedsNewRequest);
    }

    [Fact]
    public void RecordingAcceptedChipKeepsSameVuSentAndAllowsKgWithoutPriceFeedback()
    {
        var formula = Formula("SampleSent");
        var originalId = formula.FormulaId;
        var chip = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        SampleFlowRules.ApplyChipReply(request, chip, SampleTrialStatus.Approved, Guid.NewGuid(), Start.AddHours(2));
        Assert.Equal("SampleSent", formula.Status);
        Assert.Equal(originalId, chip.FormulaId);
        var blocked = SampleFlowStateReader.Build(request, chip, formula, [Plan()], true, false);
        Assert.Contains("Send", blocked.AllowedActions);
        var acceptedPrice = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted,
            FormulaId = originalId, RecordedAt = Start.AddMinutes(-1) };
        var ready = SampleFlowStateReader.Build(request, chip, formula, [Plan(), acceptedPrice], true, false);
        Assert.Equal("ReadyToSendSample", ready.Stage);
        Assert.Equal("TrialSample", ready.DeliveryKind);
        Assert.Equal(100, ready.RequestedKg);
        Assert.Equal(originalId, ready.FormulaId);
        Assert.Contains("Send", ready.AllowedActions);
    }

    [Theory]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback, "SampleSent", true)]
    [InlineData(SampleTrialStatus.Failed, "Rework", false)]
    public void ChipSendAllowsKgWithoutFeedbackButReworkStillNeedsConfirmation(SampleTrialStatus status, string formulaStatus, bool canSend)
    {
        var formula = Formula(formulaStatus);
        var state = SampleFlowStateReader.Build(request, Chip(formula, status), formula, [Plan()], true, false);
        Assert.Equal(canSend ? "ReadyToSendSample" : "PrepareSample", state.Stage);
        Assert.Equal("TrialSample", state.DeliveryKind);
        Assert.Equal(canSend, state.AllowedActions.Contains("Send"));
    }

    [Fact]
    public void ApprovedChipAdvancesToKgWithoutPriceGate()
    {
        var formula = Formula(); var chip = Chip(formula, SampleTrialStatus.Approved);
        var blocked = SampleFlowStateReader.Build(request, chip, formula, [Plan()], true, false);
        Assert.Equal("TrialSample", blocked.DeliveryKind);
        Assert.Contains("Send", blocked.AllowedActions);
        var price = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted,
            RecordedAt = Start.AddHours(2), FormulaId = formula.FormulaId };
        var ready = SampleFlowStateReader.Build(request, chip, formula, [price, Plan()], true, false);
        Assert.Equal("ReadyToSendSample", ready.Stage);
        Assert.Contains("Send", ready.AllowedActions);
        Assert.Equal(100, request.SampleQuantity);
    }

    [Fact]
    public void NewPlanNeedsANewChipButKgMayUseAnotherConfirmedFormula()
    {
        var formula = Formula(); var chip = Chip(formula, SampleTrialStatus.Approved);
        chip.CreatedDate = Start.AddDays(-1);
        Assert.True(ChipThenSampleRules.NeedsChipRecord(request, formula.FormulaId, [Plan()], [chip]));
        chip.CreatedDate = Start.AddHours(1);
        Assert.False(ChipThenSampleRules.NeedsChipRecord(request, Guid.NewGuid(), [Plan()], [chip]));
    }

    [Fact]
    public void ReusedDraftUsesDeliveryEvidenceAndApprovedHistorySurvivesANewDraft()
    {
        var formula = Formula(); var chip = Chip(formula, SampleTrialStatus.Approved);
        chip.CreatedDate = Start.AddDays(-1);
        var sent = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.SampleSent,
            RecordedAt = Start.AddHours(1), SampleRequestSampleTrialId = chip.SampleRequestSampleTrialId };
        Assert.False(ChipThenSampleRules.NeedsChipRecord(request, formula.FormulaId, [sent, Plan()], [chip]));
        var draft = new SampleRequestSampleTrial { IsActive = true, SampleRequestId = request.SampleRequestId,
            FormulaId = formula.FormulaId, Status = SampleTrialStatus.Draft };
        var state = SampleFlowStateReader.Build(request, draft, formula, [sent, Plan()], true, false, trials: [draft, chip]);
        Assert.Equal("TrialSample", state.DeliveryKind);
    }

    [Fact]
    public void ExplicitPlanChangeAndLegacyRetainTheirExistingBehaviour()
    {
        var formula = Formula();
        var kg = Plan(SampleFlowRules.KgRequested); kg.RecordedAt = Start.AddMinutes(1);
        Assert.False(ChipThenSampleRules.NeedsChipRecord(request, formula.FormulaId, [kg, Plan()], []));
        Assert.Equal("TrialSample", SampleFlowStateReader.Build(request, null, formula, [], true, false).DeliveryKind);
        request.CreatedDate = new(2026, 10, 31);
        Assert.False(ChipThenSampleRules.NeedsChipRecord(request, formula.FormulaId, [Plan()], []));
        Assert.Equal("Legacy", SampleFlowStateReader.Build(request, null, formula, [Plan()], true, false).WorkflowMode);
    }
}
