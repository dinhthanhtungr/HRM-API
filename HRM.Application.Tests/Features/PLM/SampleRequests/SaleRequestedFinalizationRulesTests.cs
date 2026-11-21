using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SaleRequestedFinalizationRulesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalizationMustKeepTheExactSaleRequestedFormulaAndActivePrice(bool chip)
    {
        var (request, trial, instruction, price) = Setup(chip);
        var action = chip ? "FinalizeChip" : "FinalizeQuotation";
        SampleRequestSampleTrial[] trials = chip ? [trial] : [];
        Assert.Null(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, action, [instruction, price], trials));
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, Guid.NewGuid(), action, [instruction, price], trials));
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, action, [price], trials));
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, action,
            [new() { EventType = SampleRequestWorkflowEvents.PriceRevoked }, instruction, price], trials));
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, action,
            [new() { EventType = SampleFlowRules.KgRequested }, instruction, price], trials));
        request.SampleQuantity = 5;
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, action, [instruction, price], trials));
    }

    [Fact]
    public void ChipMustBeAcceptedAndBelongToTheRequestedDelivery()
    {
        var (request, trial, instruction, price) = Setup(true);
        foreach (var status in new[] { SampleTrialStatus.Failed, SampleTrialStatus.WaitingCustomerFeedback })
        {
            trial.Status = status;
            Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, "FinalizeChip", [instruction, price], [trial]));
        }
        trial.Status = SampleTrialStatus.Approved;
        instruction.SampleRequestSampleTrialId = Guid.NewGuid();
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, "FinalizeChip", [instruction, price], [trial]));
        instruction.SampleRequestSampleTrialId = trial.SampleRequestSampleTrialId;
        Assert.NotNull(SaleRequestedFinalizationRules.Validate(request, trial.FormulaId, "FinalizeChip",
            [new() { EventType = SampleRequestWorkflowEvents.SampleSent }, instruction, price], [trial]));
    }

    private static (SampleRequest, SampleRequestSampleTrial, SampleRequestWorkflowEvent, SampleRequestWorkflowEvent) Setup(bool chip)
    {
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), Status = "InProgress", InfoType = "Quotation" };
        var trial = new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId,
            SampleRequestSampleTrialId = Guid.NewGuid(), FormulaId = Guid.NewGuid(), IsActive = true,
            DeliveredSampleQuantityKg = 0, SentDate = new(2026, 11, 15), Status = SampleTrialStatus.Approved, TrialNo = 1 };
        return (request, trial, new() { Id = Guid.NewGuid(), FormulaId = trial.FormulaId,
            EventType = chip ? SampleFlowRules.ChipFinalizationRequested : SampleFlowRules.LabFinalizationRequested,
            SampleRequestSampleTrialId = chip ? trial.SampleRequestSampleTrialId : null },
            new() { FormulaId = trial.FormulaId, EventType = SampleRequestWorkflowEvents.PriceAccepted });
    }
}
