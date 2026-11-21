using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class KgSampleCompletionRulesTests
{
    [Fact]
    public void PriceAcceptedOnAnotherFormulaKeepsTheSelectedSampleOption()
    {
        var selected = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial { FormulaId = selected, SentDate = DateTime.Now,
            DeliveredSampleQuantityKg = 2, Status = SampleTrialStatus.Approved };
        SampleRequestWorkflowEvent[] events = [new() { EventType = "CustomerPriceAccepted", FormulaId = Guid.NewGuid() }];
        Assert.True(KgSampleCompletionRules.IsSatisfied(trial, selected, events));
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, Guid.NewGuid(), events));
        Assert.Equal(selected, trial.FormulaId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptedKgCompletesAndSelectsItsFormulaWithoutWaitingForPrice(bool priceFirst)
    {
        var now = new DateTime(2026, 11, 10);
        var formula = new Formula { FormulaId = Guid.NewGuid(), Status = "SampleSent", IsActive = true };
        var request = new SampleRequest { CreatedDate = now, Status = "SampleSent", InfoType = "Báo giá" };
        var trial = new SampleRequestSampleTrial { Formula = formula, FormulaId = formula.FormulaId,
            SentDate = now, DeliveredSampleQuantityKg = 2, Status = SampleTrialStatus.WaitingCustomerFeedback };
        var price = new SampleRequestWorkflowEvent { EventType = "CustomerPriceAccepted", FormulaId = formula.FormulaId };
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, formula.FormulaId, priceFirst ? [price] : []));
        SampleRequestSampleTrialApprovalRules.ApplyApproved(request, trial, [formula], Guid.NewGuid(), now, "APPROVED");
        Assert.Equal("Completed", request.Status);
        Assert.Equal("Completed", formula.Status);
        Assert.True(formula.IsSelect);
        Assert.Equal(formula.FormulaId, request.FormulaId);
        Assert.Equal(priceFirst, KgSampleCompletionRules.IsSatisfied(trial, formula.FormulaId, priceFirst ? [price] : []));
        Assert.True(KgSampleCompletionRules.IsSatisfied(trial, formula.FormulaId, [price]));
    }

    [Theory]
    [InlineData("CustomerPriceAcceptanceRevoked")]
    [InlineData("CustomerPriceRejected")]
    [InlineData("CustomerPriceNoResponse")]
    public void LaterPriceOutcomePreventsCompletion(string outcome)
    {
        var id = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial { FormulaId = id, SentDate = DateTime.Now,
            DeliveredSampleQuantityKg = 1, Status = SampleTrialStatus.Approved };
        var accepted = new SampleRequestWorkflowEvent { EventType = "CustomerPriceAccepted", FormulaId = id };
        Assert.True(KgSampleCompletionRules.IsSatisfied(trial, id, [accepted]));
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, id, [new() { EventType = outcome, FormulaId = id }, accepted]));
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, Guid.NewGuid(), [accepted]));
        trial.Status = SampleTrialStatus.Failed;
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, id, [accepted]));
    }

    [Fact]
    public void UnsentInactiveOrChipTrialCannotCompleteKgRequest()
    {
        var id = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial { FormulaId = id,
            DeliveredSampleQuantityKg = 1, Status = SampleTrialStatus.Approved };
        SampleRequestWorkflowEvent[] events = [new() { EventType = "CustomerPriceAccepted", FormulaId = id }];
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, id, events));
        trial.SentDate = DateTime.Now;
        trial.IsActive = false;
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, id, events));
        trial.IsActive = true;
        trial.DeliveredSampleQuantityKg = 0;
        Assert.False(KgSampleCompletionRules.IsSatisfied(trial, id, events));
        Assert.False(KgSampleCompletionRules.IsSatisfied(null, id, events));
    }
}
