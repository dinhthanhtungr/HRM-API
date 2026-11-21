using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleFlowRulesTests
{
    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Draft", false)]
    public void NewDeliveryPlanUsesConfirmedCandidateOnSameTp(string status, bool canSend)
    {
        var request = Request(5);
        var previous = Formula(request, "SampleSent");
        var trial = Chip(previous, SampleTrialStatus.Approved);
        trial.IsActive = true;
        trial.DeliveredSampleQuantityKg = 2;
        trial.SentDate = Now.AddDays(-1);
        var candidate = Formula(request, status);
        var plan = Event(request, previous, SampleFlowRules.KgRequested);
        plan.RecordedAt = Now;
        var state = SampleFlowStateReader.Build(request, trial, candidate, [plan], true, false);
        Assert.Equal(canSend, state.AllowedActions.Contains("Send"));
        Assert.False(state.DeliveryNeedsNewRequest);
        Assert.Equal(candidate.FormulaId, state.FormulaId);
        Assert.Equal(previous.FormulaId, trial.FormulaId);
        Assert.Equal(SampleTrialStatus.Approved, trial.Status);
        Assert.Equal(Now.AddDays(-1), trial.SentDate);
    }

    [Fact]
    public void NewDeliveryPlanDoesNotBypassPendingFeedback()
    {
        var request = Request(5);
        var formula = Formula(request, "Approved");
        var trial = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        trial.IsActive = true;
        trial.DeliveredSampleQuantityKg = 2;
        var plan = Event(request, formula, SampleFlowRules.KgRequested);
        Assert.DoesNotContain("Send", SampleFlowStateReader.Build(request, trial, formula, [plan], true, false).AllowedActions);
    }
    [Fact]
    public void LegacyDeliveryRecordDoesNotRequireModernPriceOrCustomerFeedbackBeforeCompletion()
    {
        var request = Request(0.1);
        request.CreatedDate = new DateTime(2026, 10, 31);
        request.Status = "SampleSent";
        var formula = Formula(request, "SampleSent");
        var delivery = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        delivery.DeliveredSampleQuantityKg = 0.1m;
        var sale = SampleFlowStateReader.Build(request, delivery, formula, [], false, true);
        Assert.Equal("LegacySent", sale.Stage);
        Assert.Contains("Complete", sale.AllowedActions);
        Assert.DoesNotContain("PriceAccepted", sale.AllowedActions);
    }
    [Fact]
    public void RequestedKgCanBeSentEvenWhenPriceWasAcceptedFirst()
    {
        var request = Request(); request.InfoType = "Thẻ màu (1)"; request.SampleQuantity = 2; request.RealPriceQuoteDate = Now;
        var formula = Formula(request, "Approved");
        var oldChoice = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.KgRequested, RecordedAt = Now.AddMinutes(-1) };
        var accepted = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = formula.FormulaId, RecordedAt = Now };
        var state = SampleFlowStateReader.Build(request, null, formula, [accepted, oldChoice], true, true, labConfirmed: true);
        Assert.Equal("ReadyToSendSample", state.Stage);
        Assert.Contains("Send", state.AllowedActions);
        var choice = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.LabFinalizationRequested, RecordedAt = Now.AddSeconds(1) };
        Assert.Equal("FinalizeQuotationFormula", SampleFlowStateReader.Build(request, null, formula, [choice, accepted, oldChoice], true, false, labConfirmed: true).Stage);
        Assert.False(SampleFlowStateReader.NeedsAfterPriceDecision([choice, accepted, oldChoice], formula.FormulaId));
        Assert.True(SampleFlowStateReader.NeedsAfterPriceDecision([accepted, oldChoice], formula.FormulaId));
    }

    [Fact]
    public void PriceRejectionRequiresReviewUntilLabConfirmationOrRepricing()
    {
        var rejected = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceRejected, RecordedAt = Now };
        var choice = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.LowerPriceRequested, RecordedAt = Now.AddSeconds(1) };
        Assert.False(SampleFlowStateReader.NeedsPriceAdjustment([rejected]));
        Assert.True(SampleFlowStateReader.NeedsPriceAdjustment([choice, rejected]));
        Assert.False(SampleFlowStateReader.NeedsPriceAdjustment([rejected], Now.AddMinutes(1)));
        Assert.True(SampleFlowStateReader.NeedsPriceAdjustment([choice, rejected], Now.AddMinutes(-1)));
        var confirmed = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.FormulaConfirmed, RecordedAt = Now.AddMinutes(1) };
        Assert.False(SampleFlowStateReader.NeedsPriceAdjustment([confirmed, rejected]));
        var waiting = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceNoResponse, RecordedAt = Now.AddMinutes(1) };
        Assert.False(SampleFlowStateReader.NeedsPriceAdjustment([waiting, rejected]));
    }

    [Fact]
    public void RejectionDecisionDoesNotReuseChoicesFromEarlierRound()
    {
        var rejected = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceRejected, RecordedAt = Now };
        var wait = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceFollowUpDeferred, RecordedAt = Now.AddSeconds(1) };
        Assert.True(SampleFlowStateReader.NeedsPriceDecision([rejected]));
        Assert.True(SampleFlowStateReader.NeedsPriceDecision([wait, rejected]));
        Assert.True(SampleFlowStateReader.IsPriceDecisionDeferred([wait, rejected]));
        var again = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceRejected, RecordedAt = Now.AddMinutes(1) };
        Assert.True(SampleFlowStateReader.NeedsPriceDecision([again, wait, rejected]));
        Assert.False(SampleFlowStateReader.IsPriceDecisionDeferred([again, wait, rejected]));
        var request = Request(); request.InfoType = "Quotation";
        var formula = Formula(request, "Approved");
        var sale = SampleFlowStateReader.Build(request, null, formula, [rejected], false, true);
        Assert.Equal("AwaitingPriceDecision", sale.Stage);
        Assert.Equal(3, sale.AllowedActions.Count);
        Assert.Contains("CancelRejectedRequest", sale.AllowedActions);
        Assert.Equal(new[] { "RejectFormula" }, SampleFlowStateReader.Build(request, null, formula, [rejected], true, false).AllowedActions);
    }

    [Fact]
    public void SavingRevisedFormulaHandsPriceBackToPresidentUntilNewApproval()
    {
        var rejected = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.PriceRejected, RecordedAt = Now };
        var revised = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.FormulaConfirmed, RecordedAt = Now.AddMinutes(1) };
        Assert.False(SampleFlowStateReader.NeedsRevisedFormulaPrice([rejected], Now.AddMinutes(-1)));
        Assert.True(SampleFlowStateReader.NeedsRevisedFormulaPrice([revised, rejected], Now.AddMinutes(-1)));
        Assert.False(SampleFlowStateReader.NeedsRevisedFormulaPrice([revised, rejected], Now.AddMinutes(2)));
    }
    [Theory]
    [InlineData(true, false, "Completed", true)]
    [InlineData(false, false, "Completed", false)]
    [InlineData(true, true, "Completed", false)]
    [InlineData(true, false, "Approved", true)]
    public void SaleCanChooseSamplesAfterPriceAcceptanceOrLabFinalization(bool sale, bool pending, string status, bool expected)
    {
        var request = Request(); request.InfoType = "Quotation"; request.RealPriceQuoteDate = Now;
        var formula = Formula(request, status);
        var accepted = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = formula.FormulaId };
        var trial = pending ? new SampleRequestSampleTrial { Status = SampleTrialStatus.WaitingCustomerFeedback } : null;
        var state = SampleFlowStateReader.Build(request, trial, formula, [accepted], false, sale, labConfirmed: true);
        foreach (var action in new[] { "RequestChip", "RequestSample" })
            Assert.Equal(expected, state.AllowedActions.Contains(action));
        Assert.Equal(expected && status != "Completed", state.AllowedActions.Contains("RequestLabFinalization"));
        if (expected && status == "Completed") Assert.Contains("Complete", state.AllowedActions);
        if (status == "Approved") Assert.DoesNotContain("Complete", state.AllowedActions);
        Assert.Equal(status, formula.Status);
        Assert.Null(request.SampleQuantity);
    }

    [Theory]
    [InlineData("Thẻ màu (2)", 0)]
    [InlineData("Quotation", 3)]
    public void RequestedDeliveryUsesConfirmedFormulaWithoutAnotherApproval(string infoType, double kg)
    {
        var request = Request(); request.InfoType = infoType; request.SampleQuantity = kg;
        request.RealPriceQuoteDate = Now;
        var formula = Formula(request, "Approved");
        var accepted = new SampleRequestWorkflowEvent
        {
            EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = formula.FormulaId
        };
        var choice = new SampleRequestWorkflowEvent { EventType = kg > 0 ? SampleFlowRules.KgRequested : SampleFlowRules.ChipRequested, RecordedAt = Now };
        var state = SampleFlowStateReader.Build(request, null, formula, [choice, accepted], true, false, labConfirmed: true);
        Assert.Contains("Send", state.AllowedActions);
        Assert.DoesNotContain("Complete", state.AllowedActions);
    }

    [Fact]
    public void FormulaStatusDoesNotSubstituteForRecordedLabConfirmationOrApprovedPrice()
    {
        var request = Request();
        request.InfoType = "Quotation";
        var formula = Formula(request, "Approved");
        Assert.Equal("QuotationPreparation", SampleFlowStateReader.Build(request, null, formula, [], true, true).Stage);
        Assert.Equal("AwaitingStandardPrice", SampleFlowStateReader.Build(request, null, formula, [], true, true,
            labConfirmed: true).Stage);
        var published = SampleFlowStateReader.Build(request, null, formula, [], true, true,
            hasApprovedPrice: true, labConfirmed: true);
        Assert.Equal("QuotationPreparation", published.Stage);
        request.RealPriceQuoteDate = Now;
        Assert.Equal("AwaitingPriceFeedback", SampleFlowStateReader.Build(request, null, formula, [], true, true,
            hasApprovedPrice: true, labConfirmed: true).Stage);
    }

    [Fact]
    public void AcceptedQuotationRequiresLabFinalizationBeforeSaleCompletion()
    {
        var request = Request(); request.InfoType = "Quotation"; request.RealPriceQuoteDate = Now;
        var formula = Formula(request, "Approved");
        var accepted = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = formula.FormulaId };
        var sale = SampleFlowStateReader.Build(request, null, formula, [accepted], false, true, labConfirmed: true);
        Assert.Equal("AwaitingSaleAfterPrice", sale.Stage);
        Assert.Equal(new[] { "RequestLabFinalization", "RequestChip", "RequestSample", ChipThenSampleRules.Action, "WaitRequest", "CancelRejectedRequest" }, sale.AllowedActions);
        Assert.Equal(new[] { "RejectFormula" }, SampleFlowStateReader.Build(request, null, formula, [accepted], true, false, labConfirmed: true).AllowedActions);
        var choice = new SampleRequestWorkflowEvent { EventType = SampleFlowRules.LabFinalizationRequested, RecordedAt = Now };
        var lab = SampleFlowStateReader.Build(request, null, formula, [choice, accepted], true, false, labConfirmed: true);
        Assert.Equal("FinalizeQuotationFormula", lab.Stage);
        Assert.Contains("FinalizeQuotation", lab.AllowedActions);
        Assert.DoesNotContain("Complete", SampleFlowStateReader.Build(request, null, formula, [accepted], false, true, labConfirmed: true).AllowedActions);
        Assert.DoesNotContain("FinalizeQuotation", SampleFlowStateReader.Build(request, null, formula,
            [new() { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = Guid.NewGuid() }], true, false, labConfirmed: true).AllowedActions);
        formula.Status = "Completed";
        Assert.Contains("Complete", SampleFlowStateReader.Build(request, null, formula, [accepted], false, true, labConfirmed: true).AllowedActions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FormulaBasisUsesSamePriorityForPriceAndWorkflow(bool legacy)
    {
        var request = Request();
        request.CreatedDate = legacy ? Now.AddMonths(-1) : Now;
        request.FormulaId = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial { FormulaId = Guid.NewGuid() };
        var confirmed = Guid.NewGuid();
        Assert.Equal(legacy ? request.FormulaId : trial.FormulaId,
            SampleFlowStateReader.ResolveFormulaId(request, trial, confirmed));
        request.FormulaId = null;
        trial.FormulaId = null;
        Assert.Equal(confirmed, SampleFlowStateReader.ResolveFormulaId(request, trial, confirmed));
    }

    [Fact]
    public void ReopeningChipOverridesOlderFinalizationInQueueAndWorkflow()
    {
        var request = Request();
        var formula = Formula(request, "Completed");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var finalized = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = request.CompanyId,
            SampleRequestId = request.SampleRequestId, FormulaId = formula.FormulaId,
            SampleRequestSampleTrialId = chip.SampleRequestSampleTrialId, EventType = SampleFlowRules.ChipFinalized,
            RecordedAt = Now.AddMinutes(-1) };
        var reopened = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = request.CompanyId,
            SampleRequestId = request.SampleRequestId, FormulaId = formula.FormulaId,
            SampleRequestSampleTrialId = chip.SampleRequestSampleTrialId, EventType = SampleFlowRules.ChipReopened,
            RecordedAt = Now };
        var (db, store) = HRM.Application.Tests.Features.PLM.ProductionOrders.ProductionOrderTestStore
            .CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMReadDbContext>();
        store.Seed(nameof(db.SampleRequestWorkflowEvents), finalized, reopened);
        var rows = new[] { new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = chip } }.AsQueryable();
        Assert.False(SampleFlowQueueQuery.Enrich(rows, db).Single().HasFinalizedChip);
        Assert.Equal("AwaitingSaleAfterChip", SampleFlowStateReader.Build(request, chip, formula, [reopened, finalized], true, true).Stage);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void StandardPriceWorkUsesApproverCapabilityRatherThanSaleOwnership(bool approver, bool sale)
    {
        var employee = Guid.NewGuid();
        var item = new HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials.SampleRequestSampleTrialReportDto
        {
            ManagerSalesEmployeeId = employee,
            SampleRequestStatus = "InProgress",
            Flow = new("ChipAndSample", "AwaitingStandardPrice", Guid.NewGuid(), null, null, 3, [])
        };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 3), false, false, approver, sale, employee);
        Assert.Equal("ApproveStandardPrice", item.NextActionCode);
        Assert.Equal(approver, item.IsActionForCurrentUser);
    }
    private static readonly DateTime Now = new(2026, 11, 2, 10, 0, 0);
    [Theory]
    [InlineData(null, true, true, true)]
    [InlineData(2.0, true, true, true)]
    [InlineData(2.0, false, true, false)]
    [InlineData(null, true, false, false)]
    public void ParallelPriceFeedbackRequiresSaleAndApprovedPrice(double? kg, bool sale, bool approvedPrice, bool expected)
    {
        var request = Request(kg);
        request.InfoType = "Thẻ màu (1)";
        request.RealPriceQuoteDate = null;
        var formula = Formula(request, "Approved");
        var state = SampleFlowStateReader.Build(request, null, formula, [], false, sale,
            hasApprovedPrice: approvedPrice, labConfirmed: true);
        Assert.Equal(expected, state.AllowedActions.Contains("PriceRejected"));
        Assert.Equal(expected, state.AllowedActions.Contains("PriceNoResponse"));
    }

    private static SampleRequest Request(double? kg = null) => new()
    {
        SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
        CreatedDate = new DateTime(2026, 11, 1), Status = "InProgress", InfoType = "ThÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â» mÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â u (2)", SampleQuantity = kg
    };
    private static Formula Formula(SampleRequest request, string status) => new()
    {
        FormulaId = Guid.NewGuid(), CompanyId = request.CompanyId, ProductId = request.ProductId,
        Status = status, CreatedDate = Now.AddHours(-1), UpdatedDate = Now
    };
    private static SampleRequestSampleTrial Chip(Formula formula, SampleTrialStatus status) => new()
    {
        SampleRequestSampleTrialId = Guid.NewGuid(), FormulaId = formula.FormulaId, Formula = formula,
        Status = status, DeliveredSampleQuantityKg = 0, IsActive = true, SentDate = Now.AddHours(-1)
    };
    private static SampleRequestWorkflowEvent Event(SampleRequest request, Formula formula, string type,
        SampleRequestSampleTrial? trial = null) => new()
    {
        SampleRequestId = request.SampleRequestId, CompanyId = request.CompanyId, EventType = type,
        FormulaId = formula.FormulaId, RecordedAt = Now,
        SampleRequestSampleTrialId = trial?.SampleRequestSampleTrialId
    };

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public void CutoffUsesRequestCreationAndIncludesFirstNovember(int octoberDay, bool legacy)
        => Assert.Equal(legacy, SampleFlowRules.IsLegacy(new DateTime(2026, 10, 1).AddDays(octoberDay - 1)));

    [Fact]
    public void DeliveryKindNeverChangesWhenSaleAddsRequestedKg()
    {
        var request = Request();
        var formula = Formula(request, "SampleSent");
        var trial = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        request.SampleQuantity = 5;
        Assert.Equal(SampleDeliveryKind.ColorChip, trial.DeliveryKind);
        Assert.Equal(SampleDeliveryKind.Unknown, new SampleRequestSampleTrial().DeliveryKind);
        trial.DeliveredSampleQuantityKg = 5;
        Assert.Equal(SampleDeliveryKind.TrialSample, trial.DeliveryKind);
    }

    [Theory]
    [InlineData(SampleTrialStatus.Approved)]
    [InlineData(SampleTrialStatus.Failed)]
    public void ChipAcceptanceKeepsSentWhileRejectionRequestsReworkWithoutCompletingFormula(SampleTrialStatus result)
    {
        var request = Request();
        var formula = Formula(request, "SampleSent");
        var chip = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        SampleFlowRules.ApplyChipReply(request, chip, result, Guid.NewGuid(), Now);
        Assert.Equal(result == SampleTrialStatus.Approved ? "SampleSent" : "Rework", formula.Status);
        Assert.Equal("InProgress", request.Status);
        Assert.False(formula.IsSelect);
        Assert.Null(request.FormulaId);
    }

    [Fact]
    public void NoResponseKeepsChipFormulaLocked()
    {
        var request = Request();
        var formula = Formula(request, "SampleSent");
        var chip = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        SampleFlowRules.ApplyChipReply(request, chip, SampleTrialStatus.WaitingCustomerFeedback, Guid.NewGuid(), Now);
        var state = SampleFlowStateReader.Build(request, chip, formula, [], true, true);
        Assert.Equal("SampleSent", formula.Status);
        Assert.Equal("AwaitingChipFeedback", state.Stage);
        Assert.Equal(new[] { "RejectFormula" }, state.AllowedActions);
    }

    [Theory]
    [InlineData("Rework")]
    [InlineData("Approved")]
    public void FailedChipCanBeSentAgainOnSameFormulaWithoutMovingMainWorkToSale(string formulaStatus)
    {
        var request = Request();
        var formula = Formula(request, formulaStatus);
        var chip = Chip(formula, SampleTrialStatus.Failed);
        var state = SampleFlowStateReader.Build(request, chip, formula, [], true, false);
        Assert.Equal("ReworkChip", state.Stage);
        Assert.Equal(formula.FormulaId, state.FormulaId);
        Assert.Contains("Send", state.AllowedActions);
        Assert.DoesNotContain("Send", SampleFlowStateReader.Build(request, chip, formula, [], false, true).AllowedActions);

        // The actual delivery starts a new round; the prior failed round is kept.
        var next = Chip(formula, SampleTrialStatus.Draft);
        FormulaSampleSentRules.PrepareTrialForDelivery(next, formula.FormulaId, "VU-RETRY", Guid.NewGuid(), Now, 0);
        formula.Status = "SampleSent";
        request.Status = "SampleSent";
        var waiting = SampleFlowStateReader.Build(request, next, formula, [], true, true);
        Assert.Equal("AwaitingChipFeedback", waiting.Stage);
        Assert.DoesNotContain("Send", waiting.AllowedActions);
        Assert.Equal(SampleTrialStatus.Failed, chip.Status);
    }

    [Theory]
    [InlineData("Rework", false, false, SampleTrialStatus.Approved)]
    [InlineData("Completed", false, false, SampleTrialStatus.Failed)]
    [InlineData("Rework", true, false, SampleTrialStatus.Failed)]
    [InlineData("Rework", false, true, SampleTrialStatus.Failed)]
    public void ChipRetryDoesNotBypassFinalizationKgOrClosedRequest(string formulaStatus, bool requestedKg, bool closed, SampleTrialStatus status)
    {
        var request = Request(requestedKg ? 2 : null);
        if (closed) request.Status = "Completed";
        var formula = Formula(request, formulaStatus);
        var chip = Chip(formula, status);
        Assert.DoesNotContain("Send", SampleFlowStateReader.Build(request, chip, formula, [], true, false).AllowedActions);
    }

    [Theory]
    [InlineData(SampleTrialStatus.Failed, "Rework")]
    [InlineData(SampleTrialStatus.Approved, "Rework")]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback, "SampleSent")]
    public void SharedFormulaAlreadySentInKgCannotAdvertiseChipMutation(SampleTrialStatus status, string formulaStatus)
    {
        var request = Request();
        var formula = Formula(request, formulaStatus);
        var chip = Chip(formula, status);
        var state = SampleFlowStateReader.Build(request, chip, formula, [], true, true, hasSentKg: true);
        Assert.Equal("NeedsReview", state.Stage);
        Assert.Equal(new[] { "RejectFormula" }, state.AllowedActions);
    }

    [Fact]
    public void FinalizedChipCannotReopenOrSendAnotherFormulaInItsKgContinuation()
    {
        var request = Request(2);
        var formula = Formula(request, "Completed");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var finalized = new SampleRequestWorkflowEvent
        {
            EventType = SampleFlowRules.ChipFinalized, FormulaId = formula.FormulaId,
            SampleRequestSampleTrialId = chip.SampleRequestSampleTrialId
        };
        var immutable = SampleFlowStateReader.Build(request, chip, formula, [finalized], true, true, hasSentKg: true);
        Assert.Equal("NeedsReview", immutable.Stage);
        Assert.DoesNotContain("ReopenChip", immutable.AllowedActions);
        var other = Formula(request, "Completed");
        Assert.DoesNotContain("Send", SampleFlowStateReader.Build(request, chip, other,
            [new SampleRequestWorkflowEvent { EventType = SampleFlowRules.ChipFinalized, FormulaId = other.FormulaId, SampleRequestSampleTrialId = chip.SampleRequestSampleTrialId },
             new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted, FormulaId = other.FormulaId }], true, true,
            hasApprovedPrice: true, labConfirmed: true).AllowedActions);
    }

    [Fact]
    public void FailedChipAllowsAnotherApprovedFormulaForNewTrial()
    {
        var request = Request();
        var delivered = Formula(request, "Rework");
        var chip = Chip(delivered, SampleTrialStatus.Failed);
        var other = Formula(request, "Approved");
        Assert.Contains("Send", SampleFlowStateReader.Build(request, chip, other, [], true, false).AllowedActions);
        Assert.Equal(delivered.FormulaId, chip.FormulaId);
        Assert.Equal(SampleTrialStatus.Failed, chip.Status);
    }

    [Fact]
    public void OlderChipFeedbackDoesNotReopenACompletedRequestOrFormula()
    {
        var request = Request();
        request.Status = "Completed";
        var formula = Formula(request, "Completed");
        var chip = Chip(formula, SampleTrialStatus.WaitingCustomerFeedback);
        SampleFlowRules.ApplyChipReply(request, chip, SampleTrialStatus.Failed, Guid.NewGuid(), Now);
        Assert.Equal("Completed", request.Status);
        Assert.Equal("Completed", formula.Status);
    }

    [Fact]
    public void ApprovedChipRequiresLabFinalizationBeforeSaleDecision()
    {
        var request = Request();
        var formula = Formula(request, "Rework");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var choice = SampleFlowStateReader.Build(request, chip, formula, [], false, true);
        Assert.Equal("AwaitingSaleAfterChip", choice.Stage);
        Assert.Contains("RequestChipFinalization", choice.AllowedActions);
        Assert.Contains("RequestSample", choice.AllowedActions);
        Assert.Contains("WaitChipReply", choice.AllowedActions);
        Assert.Contains("CancelRejectedRequest", choice.AllowedActions);
        Assert.DoesNotContain("Complete", choice.AllowedActions);
        Assert.DoesNotContain("FinalizeChip", SampleFlowStateReader.Build(request, chip, formula, [], true, false).AllowedActions);
        var lab = SampleFlowStateReader.Build(request, chip, formula,
            [Event(request, formula, SampleFlowRules.ChipFinalizationRequested, chip)], true, false);
        Assert.Equal("FinalizeChipFormula", lab.Stage);
        Assert.Contains("FinalizeChip", lab.AllowedActions);
        Assert.DoesNotContain("Complete", lab.AllowedActions);
        formula.Status = "Completed";
        var state = SampleFlowStateReader.Build(request, chip, formula,
            [Event(request, formula, SampleFlowRules.ChipFinalized, chip)], false, true);
        Assert.Equal("AwaitingSaleDecision", state.Stage);
        Assert.Contains("Complete", state.AllowedActions);
        Assert.Contains("RequestChip", state.AllowedActions);
        Assert.Contains("RequestSample", state.AllowedActions);
        Assert.Contains("WaitRequest", state.AllowedActions);
        var labActions = SampleFlowStateReader.Build(request, chip, formula,
            [Event(request, formula, SampleFlowRules.ChipFinalized, chip)], true, false).AllowedActions;
        Assert.Contains("ReopenChip", labActions);
        Assert.DoesNotContain("Complete", labActions);
    }

    [Fact]
    public void ChipFollowUpStaysWithSaleAndChoiceCannotLeakFromAnotherTrial()
    {
        var request = Request();
        var formula = Formula(request, "Rework");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var deferred = Event(request, formula, SampleFlowRules.ChipFollowUpDeferred, chip);
        var state = SampleFlowStateReader.Build(request, chip, formula, [deferred], true, true);
        Assert.Equal("ChipFollowUp", state.Stage);
        Assert.Contains("RequestChipFinalization", state.AllowedActions);
        Assert.DoesNotContain("FinalizeChip", state.AllowedActions);
        Assert.DoesNotContain("Complete", state.AllowedActions);
        deferred.SampleRequestSampleTrialId = Guid.NewGuid();
        Assert.Equal("AwaitingSaleAfterChip", SampleFlowStateReader.Build(request, chip, formula, [deferred], true, true).Stage);
        var requested = Event(request, formula, SampleFlowRules.ChipFinalizationRequested, chip);
        requested.FormulaId = Guid.NewGuid();
        Assert.Equal("AwaitingSaleAfterChip", SampleFlowStateReader.Build(request, chip, formula, [requested], true, true).Stage);
    }

    [Fact]
    public void ChipOrderingBranchCompletesWithoutDeliveryButSaleMayChangeItsRequest()
    {
        var request = Request();
        var formula = Formula(request, "Rework");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var requested = Event(request, formula, SampleFlowRules.ChipFinalizationRequested, chip);
        var lab = SampleFlowStateReader.Build(request, chip, formula, [requested], true, false);
        Assert.Equal(new[] { "RejectFormula", "FinalizeChip" }, lab.AllowedActions);
        formula.Status = "Completed";
        var finalized = Event(request, formula, SampleFlowRules.ChipFinalized, chip);
        var sale = SampleFlowStateReader.Build(request, chip, formula, [finalized, requested], false, true);
        Assert.Contains("Complete", sale.AllowedActions);
        Assert.Contains("RequestSample", sale.AllowedActions);
        Assert.Contains("RequestChip", sale.AllowedActions);
        Assert.Contains("WaitRequest", sale.AllowedActions);
        Assert.DoesNotContain("Send", sale.AllowedActions);
        Assert.Equal(SampleTrialStatus.Approved, chip.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleCanChangePendingOrCompletedChipFinalizationAndWithdrawIt(bool completed)
    {
        var request = Request();
        var formula = Formula(request, completed ? "Completed" : "Rework");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var requested = Event(request, formula, SampleFlowRules.ChipFinalizationRequested, chip);
        var events = completed ? new[] { Event(request, formula, SampleFlowRules.ChipFinalized, chip), requested } : new[] { requested };
        var sale = SampleFlowStateReader.Build(request, chip, formula, events, false, true);
        foreach (var action in new[] { "RequestChip", "RequestSample", "WaitRequest", "CancelRejectedRequest" }) Assert.Contains(action, sale.AllowedActions);
        var withdrawn = Event(request, formula, SampleFlowRules.CompletionDeferred, chip);
        var deferred = SampleFlowStateReader.Build(request, chip, formula, new[] { withdrawn }.Concat(events).ToArray(), true, true);
        Assert.Equal("RequestFollowUp", deferred.Stage);
        Assert.DoesNotContain("FinalizeChip", deferred.AllowedActions);
        Assert.Contains("RequestChipFinalization", deferred.AllowedActions);
        var resend = Event(request, formula, SampleFlowRules.ChipRequested, chip);
        formula.Status = "Approved";
        var delivery = SampleFlowStateReader.Build(request, chip, formula, new[] { resend, withdrawn }.Concat(events).ToArray(), true, false);
        Assert.Equal("PrepareChip", delivery.Stage);
        Assert.Contains("Send", delivery.AllowedActions);
        Assert.DoesNotContain("FinalizeChip", delivery.AllowedActions);
        Assert.Empty(SampleFlowStateReader.Build(request, chip, formula, events, false, false).AllowedActions);
        request.Status = "Completed";
        Assert.Empty(SampleFlowStateReader.Build(request, chip, formula, events, false, true).AllowedActions);
    }

    [Fact]
    public void QuotationCompletionMayBeDeferredOrChangedWithoutChangingPriceAcceptance()
    {
        var request = Request();
        request.InfoType = "Báo giá";
        request.RealPriceQuoteDate = Now;
        var formula = Formula(request, "Approved");
        var price = Event(request, formula, SampleRequestWorkflowEvents.PriceAccepted);
        var requested = Event(request, formula, SampleFlowRules.LabFinalizationRequested);
        var deferred = Event(request, formula, SampleFlowRules.CompletionDeferred);
        var initial = SampleFlowStateReader.Build(request, null, formula, [price], true, true, hasApprovedPrice: true, labConfirmed: true);
        Assert.Equal("AwaitingSaleAfterPrice", initial.Stage);
        Assert.Contains("WaitRequest", initial.AllowedActions);
        Assert.Contains("RequestLabFinalization", initial.AllowedActions);
        Assert.DoesNotContain("FinalizeQuotation", initial.AllowedActions);
        var state = SampleFlowStateReader.Build(request, null, formula, [requested, price], false, true, hasApprovedPrice: true, labConfirmed: true);
        Assert.Equal("FinalizeQuotationFormula", state.Stage);
        Assert.Contains("WaitRequest", state.AllowedActions);
        var waiting = SampleFlowStateReader.Build(request, null, formula, [deferred, requested, price], true, true, hasApprovedPrice: true, labConfirmed: true);
        Assert.Equal("RequestFollowUp", waiting.Stage);
        Assert.DoesNotContain("FinalizeQuotation", waiting.AllowedActions);
        Assert.Contains("RequestLabFinalization", waiting.AllowedActions);
        Assert.True(SampleFlowStateReader.ActivePrice([deferred, requested, price], formula.FormulaId));
    }

    [Fact]
    public void FinalizedChipCanSendKgWithoutPriceAcceptance()
    {
        var request = Request(5);
        var formula = Formula(request, "Completed");
        var chip = Chip(formula, SampleTrialStatus.Approved);
        var finalized = Event(request, formula, SampleFlowRules.ChipFinalized, chip);
        var state = SampleFlowStateReader.Build(request, chip, formula, [finalized], true, true, hasApprovedPrice: true);
        Assert.Equal("ReadyToSendSample", state.Stage);
        Assert.Contains("Send", state.AllowedActions);
        var accepted = Event(request, formula, "CustomerPriceAccepted");
        state = SampleFlowStateReader.Build(request, chip, formula, [accepted, finalized], true, false);
        Assert.Equal("ReadyToSendSample", state.Stage);
        Assert.Contains("Send", state.AllowedActions);
        accepted.FormulaId = Guid.NewGuid();
        Assert.Contains("Send", SampleFlowStateReader.Build(request, chip, formula, [accepted, finalized], true, false).AllowedActions);
    }

    [Fact]
    public void PriceRejectionOverridesEarlierAcceptance()
    {
        var request = Request(5);
        var formula = Formula(request, "Approved");
        var accepted = Event(request, formula, "CustomerPriceAccepted");
        var rejected = Event(request, formula, SampleFlowRules.PriceRejected);
        Assert.DoesNotContain("Send", SampleFlowStateReader.Build(request, null, formula, [rejected, accepted], true, true).AllowedActions);
    }

    [Fact]
    public void ChipCanBeSentWithoutAnyPriceAcceptance()
    {
        var request = Request();
        var formula = Formula(request, "Approved");
        Assert.Contains("Send", SampleFlowStateReader.Build(request, null, formula, [], true, false).AllowedActions);
    }

    [Fact]
    public void FailedKgNeverGetsChipReworkOrCompletionActions()
    {
        var request = Request(5);
        var formula = Formula(request, "SampleSent");
        var trial = Chip(formula, SampleTrialStatus.Failed);
        trial.DeliveredSampleQuantityKg = 5;
        var state = SampleFlowStateReader.Build(request, trial, formula, [], true, true);
        Assert.Equal("ReworkSample", state.Stage);
        Assert.DoesNotContain("Complete", state.AllowedActions);
        Assert.DoesNotContain("ReopenChip", state.AllowedActions);
        Assert.Contains("CancelRejectedRequest", state.AllowedActions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedKgCanSendConfirmedFormulaAgainOnSameRequest(bool anotherFormula)
    {
        var request = Request(5);
        var previous = Formula(request, "Rework");
        var trial = Chip(previous, SampleTrialStatus.Failed);
        trial.DeliveredSampleQuantityKg = 5;
        trial.SentDate = Now.AddDays(-1);
        var formula = anotherFormula ? Formula(request, "Approved") : previous;
        formula.Status = "Approved";
        var state = SampleFlowStateReader.Build(request, trial, formula, [], true, false, hasSentKg: !anotherFormula);
        Assert.Contains("Send", state.AllowedActions);
        Assert.DoesNotContain("Complete", state.AllowedActions);
        Assert.Equal(formula.FormulaId, SampleFlowStateReader.ResolveFormulaId(request, trial, formula.FormulaId));
        Assert.Equal(SampleTrialStatus.Failed, trial.Status);
    }

    [Fact]
    public void LegacySendDoesNotRequireKgPriceOrTrial()
    {
        var request = Request();
        request.CreatedDate = Now.AddMonths(-1);
        var formula = Formula(request, "Approved");
        var state = SampleFlowStateReader.Build(request, null, formula, [], true, false);
        Assert.Equal("Legacy", state.WorkflowMode);
        Assert.Contains("Send", state.AllowedActions);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void EndedRequestsCannotSendOrReopen(string status)
    {
        var request = Request(5);
        request.Status = status;
        Assert.Empty(SampleFlowStateReader.Build(request, null, Formula(request, "Approved"), [], true, true).AllowedActions);
    }

    [Theory]
    [InlineData("LabUser", true, false)]
    [InlineData("LabAdmin", true, false)]
    [InlineData("SaleUser", false, true)]
    [InlineData("KHOUser", false, false)]
    public void LabAndSaleCapabilitiesAreSeparate(string role, bool lab, bool sale)
    {
        var permissions = new HRM.Application.Commons.Authorization.CurrentUserPermissionService(new SampleTrialFeedbackCorrectionTests.User(role));
        Assert.Equal(lab, permissions.HasPermission(HRM.Application.Commons.Authorization.ApplicationPermissions.PLM.ManageSampleLabWorkflow));
        Assert.Equal(sale, permissions.HasPermission(HRM.Application.Commons.Authorization.ApplicationPermissions.PLM.ManageSampleSaleWorkflow));
    }

    [Fact]
    public async Task AnonymousActionFailsBeforeDatabaseAccess()
    {
        var handler = new HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction.ApplySampleFlowActionCommandHandler(
            null!, new SampleTrialFeedbackCorrectionTests.User("SaleUser", false), null!, null!, null!, null!, null!);
        Assert.False((await handler.Handle(new(), CancellationToken.None)).Success);
    }

    [Theory]
    [InlineData("Complete")]
    [InlineData("RequestChipThenSample")]
    public async Task CrossCompanyAndOtherSaleCannotCompleteOrRequestKg(string action)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = HRM.Application.Tests.Features.PLM.ProductionOrders.ProductionOrderTestStore.CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMWriteDbContext>();
        var request = Request();
        request.CompanyId = user.CompanyId!.Value;
        request.ManagerBy = Guid.NewGuid();
        store.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        store.Seed(nameof(db.SampleRequests), request);
        var handler = new HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction.ApplySampleFlowActionCommandHandler(db, user,
            new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user), SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!);
        var command = new HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction.ApplySampleFlowActionCommand { SampleRequestId = request.SampleRequestId, Action = action };
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        request.CompanyId = Guid.NewGuid();
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void QueuePredicatesTranslateBeforePaginationAndDerivedKindAddsNoColumn()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var rows = from request in db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
                   join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
                   from trial in trials.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial };
        rows = SampleRequestLabWorkQuery.Enrich(rows, db);
        rows = HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkDecisionQuery.Enrich(rows, db, db, GetSampleRequestSampleTrialsQuery.DefaultCurrency);
        foreach (var audience in new[] { SampleRequestWorkAudience.Lab, SampleRequestWorkAudience.Sale })
        {
            var query = SampleRequestWorkQueueQuery.Apply(rows,
                new GetSampleRequestSampleTrialsQuery { WorkAudience = audience, WorkBucket = SampleRequestWorkBucket.ActionRequired }, null!);
            var sql = query.OrderBy(x => x.SampleRequest.SampleRequestId).Take(15).ToQueryString();
            Assert.Contains("LIMIT", sql);
            Assert.Contains("CustomerPriceAccepted", sql);
            Assert.Contains(SampleFlowRules.ChipThenSampleRequested, sql);
        }
        Assert.Null(db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!.FindProperty(nameof(SampleRequestSampleTrial.DeliveryKind)));
        Assert.True(db.Model.FindEntityType(typeof(Formula))!.FindProperty(nameof(HRM.Domain.Entities.SampleRequestSchema.Formula.UpdatedDate))!.IsConcurrencyToken);
    }
}
