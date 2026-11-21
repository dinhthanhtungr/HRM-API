using HRM.Application.Features.Development.SampleWorkflowSimulation;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkflowSimulationTests
{
    [Fact]
    public void BatchFeedbackSavesAtomicallyAndOneResponseIsEnoughToRecordTheBatch()
    {
        var initial = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback" });
        var changes = new[] { new SimulationFeedbackInput("option-1-1", "Failed", "2026-11-15", "Needs adjustment") };
        var failed = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", State = initial.State, ActionId = "SampleFeedback:1", Input = new() { Feedbacks = changes } });
        Assert.Equal("Failed", failed.State.Batches[0].Options[0].Result);
        Assert.Equal("", failed.State.Batches[0].Options[1].Result);
        Assert.DoesNotContain(failed.PendingSale, work => work.Vi.Contains("đợt 1"));
        Assert.False(failed.Actions.Single(a => a.Id == "Complete").Enabled);
        Assert.Contains(failed.Actions, a => a.Id == "RequestKg"); Assert.Contains(failed.Actions, a => a.Id == "Cancel");
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", State = initial.State, ActionId = "SampleFeedback:1", Input = new() {
            Feedbacks = [changes[0], new("option-1-2", "Approved", "2020-01-01", "Invalid date")] } }));
        Assert.All(initial.State.Batches[0].Options, option => Assert.Equal("", option.Result));
        var winner = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", State = failed.State, ActionId = "SampleFeedback:1", Input = new() {
            Feedbacks = [new("option-1-2", "Approved", "2026-11-15", "Selected")] } });
        Assert.Single(winner.State.Batches[0].Options, o => o.Result == "Approved");
        Assert.Equal(2, winner.State.Batches[0].Options.Count(o => o.Result == "Failed"));
    }
    [Fact]
    public void CompletionUsesTheRequestedCombinationAndRemainsVisibleWhenBlocked()
    {
        var priceOnly = SimulationTransitions.Evaluate(new() { ScenarioId = "empty", State = new() { PriceAccepted = true } });
        Assert.True(priceOnly.Actions.Single(a => a.Id == "Complete").Enabled);
        var chip = SimulationTransitions.Evaluate(new() { ScenarioId = "chip", State = new() { PriceAccepted = true, ChipSent = true, ChipRequired = true } });
        Assert.False(chip.Actions.Single(a => a.Id == "Complete").Enabled);
        Assert.NotEmpty(chip.Actions.Single(a => a.Id == "Complete").DisabledReasons);
        Assert.Equal("Outstanding", chip.Actions.Single(a => a.Id == "ChipFeedback").Group);
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "chip", State = chip.State, ActionId = "Complete" }));
        chip = SimulationTransitions.Evaluate(new() { ScenarioId = "chip", State = chip.State, ActionId = "ChipFeedback", Input = new() { Result = "Approved", Date = "2026-11-15" } });
        Assert.True(chip.Actions.Single(a => a.Id == "Complete").Enabled);
        var ready = SimulationCatalog.RegressionFixtures().Single(c => c.Id == "ready").State with { Combined = true, ChipSent = true, ChipFeedbackDone = false };
        var both = SimulationTransitions.Evaluate(new() { ScenarioId = "ready", State = ready });
        Assert.True(both.Actions.Single(a => a.Id == "Complete").Enabled);
    }
    [Theory]
    [InlineData("Sale")]
    [InlineData("Lab")]
    public void EitherRoleCanRejectWithReasonAndResumeWithoutErasingWork(string role)
    {
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = role, ActionId = "Reject" }));
        var rejected = SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = role, ActionId = "Reject", Input = new() { Note = "Cannot proceed yet" } });
        Assert.Equal("Rejected", rejected.State.Status); Assert.Equal("Cannot proceed yet", rejected.State.RejectionReason);
        Assert.Empty(rejected.PendingLab); Assert.Single(rejected.State.Batches);
        var resumed = SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = role, State = rejected.State, ActionId = "Resume" });
        Assert.Equal("Open", resumed.State.Status); Assert.Equal(10, resumed.State.SampleKg); Assert.Single(resumed.State.Batches);
        Assert.Equal(2, resumed.State.History.Count); Assert.Equal("Cannot proceed yet", resumed.State.History[0].Note);
    }
    [Fact]
    public void CompletedSupplementStartsInOneSaveAndDraftsNeedAConfirmedFirstFormula()
    {
        var extra = SimulationTransitions.Evaluate(new() { ScenarioId = "completed", ActionId = "RequestKg", Input = new() { Kg = 12 } });
        Assert.Equal("Open", extra.State.Status); Assert.Single(extra.State.ArchivedBatches); Assert.Empty(extra.State.Batches);
        Assert.Equal(12, extra.State.SampleKg); Assert.True(extra.State.PriceAccepted);
        var candidates = SimulationCatalog.Candidates(); var draft = candidates.Single(c => !c.Confirmed);
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "kg", Audience = "Lab", ActionId = "SendKg", Input = new() { Date = "2026-11-15", Options = [draft] } }));
        var sent = SimulationTransitions.Evaluate(new() { ScenarioId = "kg", Audience = "Lab", ActionId = "SendKg", Input = new() { Date = "2026-11-15", Options = [candidates[0], draft] } });
        Assert.Equal(2, sent.State.Batches[0].Options.Count); Assert.False(sent.State.Batches[0].Options[1].Confirmed);
    }
    [Fact]
    public void AcceptedPriceCanEditOnlyItsDateWhenPermittedWithoutCreatingAnotherAcceptance()
    {
        var view = SimulationTransitions.Evaluate(new() { ScenarioId = "ready", Audience = "Sale", ActionId = "ViewPriceFeedback",
            Input = new() { Result = "Approved", Date = "2026-11-14", QuotationFeedback = new("2026-11-11", "Accepted", "2026-11-14", "", "", "", "") } });
        Assert.True(view.State.PriceAccepted); Assert.False(view.State.PricePending);
        Assert.Equal("2026-11-11", view.Quotation.QuotedAt);
        Assert.Contains(view.Actions, action => action.Id == "Complete");
        Assert.DoesNotContain(view.Actions, action => action.Id == "PriceFeedback");
    }
    [Theory]
    [InlineData("price-locked", "ViewPriceFeedback", "Approved")]
    [InlineData("price-readonly", "PriceFeedback", "Approved")]
    [InlineData("price-date-only", "PriceFeedback", "Approved")]
    public void QuotationPermissionsAreEnforcedAgainWithoutDatabase(string id, string action, string result)
    {
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = "Sale", ActionId = action,
            Input = new() { Date = "2026-11-14", Result = result, QuotationFeedback = new("2026-11-10", "Accepted", "2026-11-14", "", "", "Changed", "") } }));
    }
    [Fact]
    public void DateOnlySaveAndLinkedQuotationSelectionPreserveSyntheticContext()
    {
        var dateOnly = SimulationTransitions.Evaluate(new() { ScenarioId = "price-date-only", Audience = "Sale", ActionId = "PriceFeedback", Input = new() {
            Date = "2026-11-11", Result = "Waiting", QuotationFeedback = new("2026-11-11", "NoResponse", "2026-11-15", "", "", "", "") } });
        Assert.Equal("2026-11-11", dateOnly.Quotation.QuotedAt); Assert.True(dateOnly.State.PricePending);
        var linked = SimulationTransitions.Evaluate(new() { ScenarioId = "price-linked", Audience = "Sale", ActionId = "PriceFeedback", Input = new() {
            Date = "2026-11-14", Result = "Failed", QuotationFeedback = new("2026-11-10", "Rejected", "2026-11-14", "BBG_SIM_002 · WP31070C", "sim-line-2", "Rejected second quote", "Internal") } });
        Assert.Equal("sim-line-2", linked.State.QuotationFeedback!.QuotationLineId);
        Assert.Equal(2, linked.Quotation.PriceReferences.Count);
        Assert.DoesNotContain("Internal", linked.Quotation.PreviousNote!);
    }
    [Fact]
    public void QuotationFormStoresBothDatesAndSeparateNotesWithoutChangingPendingLabWork()
    {
        var feedback = new SimulationQuotationFeedback("2026-11-10", "Accepted", "2026-11-14", "", "", "Customer accepted", "Internal Lab exchange");
        var view = SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = "Sale", ActionId = "PriceFeedback",
            Input = new() { Date = "2026-11-14", Result = "Approved", QuotationFeedback = feedback } });
        Assert.Equal(feedback, view.State.QuotationFeedback);
        Assert.True(view.State.PriceAccepted);
        Assert.Equal(10, view.State.SampleKg);
        Assert.NotEmpty(view.State.Batches);
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = "Sale", ActionId = "PriceFeedback",
            Input = new() { Date = "2026-11-14", Result = "Approved", QuotationFeedback = feedback with { FeedbackDate = "2026-11-01" } } }));
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", Audience = "Sale", ActionId = "PriceFeedback",
            Input = new() { Date = "2026-11-14", Result = "Approved", QuotationFeedback = feedback with { Note = "" } } }));
    }
    public static IEnumerable<object[]> Cases() => SimulationCatalog.RegressionFixtures().SelectMany(c => new[] { "Sale", "Lab" }.Select(role => new object[] { c.Id, role }));
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryScenarioHasConsistentOutsideAndDialogActions(string id, string audience)
    {
        var view = SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = audience });
        Assert.Equal(id, view.ScenarioId);
        Assert.Equal(view.Actions.Count, view.Actions.Select(a => a.Id).Distinct().Count());
        if (view.PrimaryAction is not null) Assert.Contains(view.PrimaryAction, view.Actions);
        if (view.State.Status == "Review") Assert.Empty(view.Actions);
        if (view.State.Status == "Cancelled") Assert.Contains(view.Actions, a => a.Id == "Resume");
        if (audience == "Sale" && view.State.ChipSets > 0) Assert.DoesNotContain(view.Actions, a => a.Id is "RequestChip" or "RequestCombined");
        if (audience == "Sale" && view.State.SampleKg > 0) Assert.DoesNotContain(view.Actions, a => a.Id is "RequestKg" or "RequestCombined");
        if (audience == "Sale" && view.State.QuotePending) Assert.DoesNotContain(view.Actions, a => a.Id == "RequestQuote");
    }
    [Fact]
    public void NewWorkDoesNotEraseEarlierFeedbackAndLabCanSendKgAfterChipWithoutFeedback()
    {
        var sale = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", Audience = "Sale", ActionId = "RequestCombined", Input = new() { Sets = 3, Kg = 10 } });
        Assert.Contains(sale.Actions, a => a.Id == "SampleFeedback:1"); Assert.True(sale.State.PricePending);
        var lab = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", Audience = "Lab", State = sale.State });
        Assert.Contains(lab.Actions, a => a.Id == "SendChip"); Assert.DoesNotContain(lab.Actions, a => a.Id == "SendKg");
        lab = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", Audience = "Lab", State = lab.State, ActionId = "SendChip", Input = new() { Sets = 3, Date = "2026-11-14" } });
        Assert.False(lab.State.ChipFeedbackDone); Assert.Contains(lab.Actions, a => a.Id == "SendKg");
        lab = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", Audience = "Lab", State = lab.State, ActionId = "SendKg", Input = new() { Date = "2026-11-14", Options = SimulationCatalog.Candidates() } });
        Assert.Equal(2, lab.State.Batches.Count); Assert.Equal(3, lab.State.Batches[1].Options.Count);
        Assert.Equal("OldBatchPolicy", lab.State.UnresolvedRule);
        var result = SimulationTransitions.Evaluate(new() { ScenarioId = "feedback", Audience = "Sale", State = lab.State, ActionId = "SampleFeedback:1", Input = new() { OptionId = "option-1-2", Result = "Approved", Date = "2026-11-14", Note = "Selected" } });
        Assert.Equal(1, result.State.Batches[0].Options.Count(o => o.Result == "Approved"));
        Assert.Equal(2, result.State.Batches[0].Options.Count(o => o.Result == "Failed"));
        Assert.All(result.State.Batches[1].Options, o => Assert.Equal("", o.Result));
    }
    [Fact]
    public void CompletionAndCancelledStatesAreEnforcedAgainByBackend()
    {
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "parallel", ActionId = "Complete" }));
        var done = SimulationTransitions.Evaluate(new() { ScenarioId = "ready", ActionId = "Complete" });
        Assert.Equal("Completed", done.State.Status);
        var resumed = SimulationTransitions.Evaluate(new() { ScenarioId = "ready", State = done.State, ActionId = "Supplement" });
        Assert.Single(resumed.State.ArchivedBatches); Assert.Empty(resumed.State.Batches);
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "cancelled", ActionId = "RequestKg", Input = new() { Kg = 10 } }));
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "kg", ActionId = "RequestChip", Input = new() { Sets = 3 } }));
    }
    [Fact]
    public void CatalogIsFreshAndInvalidInputsCannotMutateSharedCases()
    {
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "empty", ActionId = "RequestKg", Input = new() { Kg = -1 } }));
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { ScenarioId = "missing" }));
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { Audience = "President" }));
        Assert.Throws<ArgumentException>(() => SimulationTransitions.Evaluate(new() { State = new() { Batches = null! } }));
        SimulationTransitions.Evaluate(new() { ScenarioId = "empty", ActionId = "RequestKg", Input = new() { Kg = 5 } });
        Assert.Equal(0, SimulationCatalog.RegressionFixtures().Single(s => s.Id == "empty").State.SampleKg);
    }
    [Theory]
    [InlineData("SaleUser", true, false)]
    [InlineData("LabUser", true, false)]
    [InlineData("Developer", false, false)]
    [InlineData("Developer", true, true)]
    public async Task HandlerRequiresRealDeveloperEvenWhenSimulatedAudienceChanges(string role, bool authenticated, bool expected)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role, authenticated);
        var scenarios = await new GetSimulationScenariosQueryHandler(user).Handle(new(), default);
        var result = await new EvaluateSimulationCommandHandler(user).Handle(new(new() { Audience = "Lab" }), default);
        Assert.Equal(expected, scenarios is not null); Assert.Equal(expected, result is not null);
    }
}
