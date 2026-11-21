using HRM.Application.Features.Development.SampleWorkflowSimulation;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class MainFeedbackRequirementsTests
{
    [Fact]
    public void LabFinalizationUsesThePriceAcceptedFormulaInsteadOfANewerConfirmedFormula()
    {
        var accepted = Guid.NewGuid(); var newer = Guid.NewGuid();
        var request = new SampleRequest { CreatedDate = new(2026, 11, 15), InfoType = "Quotation", FormulaId = newer };
        SampleRequestWorkflowEvent[] events = [
            new() { EventType = SampleFlowRules.LabFinalizationRequested, FormulaId = accepted },
            new() { EventType = "CustomerPriceAccepted", FormulaId = accepted },
        ];
        Assert.Equal(accepted, SampleFlowStateReader.ResolveFormulaId(request, null, newer, events));
        Assert.Equal(newer, SampleFlowStateReader.ResolveFormulaId(request, null, newer,
            [new() { EventType = "CustomerPriceAcceptanceRevoked", FormulaId = accepted }, .. events]));
        Assert.Equal(newer, SampleFlowStateReader.ResolveFormulaId(request, null, newer,
            [new() { EventType = SampleFlowRules.KgRequested, FormulaId = newer }, .. events]));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationAfterPriceAcceptanceIsIndependentOfHiddenCompletionAndStillRequiresSaleAccess(bool sale)
    {
        var now = new DateTime(2026, 11, 15);
        var formula = new Formula { FormulaId = Guid.NewGuid(), Status = "Approved", IsActive = true };
        var request = new SampleRequest { CreatedDate = now, Status = "InProgress", InfoType = "Quotation", RealPriceQuoteDate = now };
        SampleRequestWorkflowEvent[] events = [new() { EventType = "CustomerPriceAccepted", FormulaId = formula.FormulaId, RecordedAt = now }];
        var flow = SampleFlowStateReader.Build(request, null, formula, events, false, sale, hasApprovedPrice: true, labConfirmed: true);
        Assert.Equal("AwaitingSaleAfterPrice", flow.Stage);
        Assert.False(flow.CompletionVisible);
        Assert.Equal(sale, flow.AllowedActions.Contains("CancelRejectedRequest"));
        Assert.DoesNotContain("Complete", flow.AllowedActions);
    }
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ProductionKgCompletionIgnoresUnansweredChipButRequiresKgAndPrice(bool price, bool kg, bool complete)
    {
        var formula = new Formula { FormulaId = Guid.NewGuid(), Status = "SampleSent", IsActive = true };
        var request = new SampleRequest { CreatedDate = new(2026, 11, 15), Status = "SampleSent",
            InfoType = "Quotation", SampleQuantity = 10, RealPriceQuoteDate = new(2026, 11, 15) };
        var chip = new SampleRequestSampleTrial { FormulaId = formula.FormulaId, IsActive = true,
            SentDate = new(2026, 11, 15), DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.WaitingCustomerFeedback };
        var sample = new SampleRequestSampleTrial { FormulaId = formula.FormulaId, IsActive = true,
            SentDate = new(2026, 11, 15), DeliveredSampleQuantityKg = 2,
            Status = kg ? SampleTrialStatus.Approved : SampleTrialStatus.WaitingCustomerFeedback };
        SampleRequestWorkflowEvent[] events = price ? [new() { EventType = "CustomerPriceAccepted", FormulaId = formula.FormulaId }] : [];
        var flow = SampleFlowStateReader.Build(request, sample, formula, events, false, true,
            hasApprovedPrice: true, labConfirmed: true, hasSentKg: true, trials: [chip, sample]);
        Assert.Equal(complete, flow.AllowedActions.Contains("Complete"));
        Assert.Equal(!price, flow.CompletionBlockers.Contains("PriceAcceptanceRequired"));
        Assert.Equal(!kg, flow.CompletionBlockers.Contains("SampleAcceptanceRequired"));
        Assert.DoesNotContain("ChipAcceptanceRequired", flow.CompletionBlockers);
        if (complete) Assert.Empty(flow.CompletionBlockers);
    }
    [Theory]
    [InlineData(false, "PriceFeedback")]
    [InlineData(true, "CompleteRequest")]
    public void FinalizedAcceptedChipStillRequiresAcceptedPrice(bool priceAccepted, string expected)
    {
        var decision = SampleWorkDecisionRules.Resolve(new SampleWorkFacts {
            Open = true, HasTrial = true, Chip = true, Approved = true,
            FormulaStatus = "Completed", FinalizedChip = true, LabConfirmed = true,
            RequiresPriceAcceptance = true, AcceptedPrice = priceAccepted,
            ApprovedPrice = true, Quoted = true,
        });
        Assert.Equal(expected, decision.Code);
    }

    [Theory]
    [InlineData("chip-finalize")]
    [InlineData("sample-approved")]
    [InlineData("both-sample-approved")]
    [InlineData("quote-finalize")]
    public void BusinessSimulatorAndProductionDoNotCompleteWithoutCurrentPrice(string id)
    {
        var initial = SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = "Sale" });
        var workflow = initial.State.Workflow!;
        var state = initial.State with { Workflow = workflow with {
            Events = workflow.Events.Where(e => e != "CustomerPriceAccepted").ToArray(),
        }, QuotationFeedback = null };
        var view = SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = "Sale", State = state });
        Assert.DoesNotContain(view.Actions, a => a.Id == "Complete" && a.Enabled);
        Assert.NotEmpty(view.CompletionBlockers);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ChipWithoutKgRequiresBothAcceptedResults(bool price, bool chip, bool complete)
    {
        var view = SimulationDecisions.Build("test", "Sale", new() {
            ChipSent = true, ChipRequired = true, PriceAccepted = price,
            ChipResult = chip ? "Approved" : "Failed", ChipFeedbackDone = chip,
        });
        Assert.Equal(complete, Assert.Single(view.Actions, a => a.Id == "Complete").Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KgAcceptanceAndPriceSufficeRegardlessOfChipFeedback(bool chipAccepted)
    {
        var view = SimulationDecisions.Build("test", "Sale", new() {
            ChipSent = true, Combined = true, PriceAccepted = true,
            ChipResult = chipAccepted ? "Approved" : "", SampleRequired = true,
            Batches = [new(1, "2026-11-14", [new("1", "VU1", 2, "Approved")])],
        });
        Assert.True(Assert.Single(view.Actions, a => a.Id == "Complete").Enabled);
    }
}
