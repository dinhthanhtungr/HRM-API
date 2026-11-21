using HRM.Application.Features.Development.SampleWorkflowSimulation;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class BusinessSimulationTests
{
    [Fact]
    public void ChangingFormulaKeepsTheRequestPriceAccepted()
    {
        var view = Read("quote-price-accepted");
        var workflow = view.State.Workflow!;
        var changed = Read(view.ScenarioId, state: view.State with { Workflow = workflow with {
            FormulaStarts = [.. workflow.FormulaStarts, workflow.Events.Count],
            Events = [.. workflow.Events, "LabFormulaConfirmed"] } });
        Assert.True(changed.State.PriceAccepted);
        Assert.False(changed.State.PricePending);
        Assert.DoesNotContain(changed.Actions, a => a.Id == "PriceFeedback");
    }

    [Fact]
    public void AcceptedChipExposesFinalizationAsOutstandingNoteOnlyWork()
    {
        var view = Act(Read("chip-sent"), "ChipFeedback",
            new() { Result = "Approved", Date = "2026-11-15", Note = "Customer accepted chip" });
        var action = Assert.Single(view.Actions, a => a.Id == "RequestChipFinalization");
        Assert.Equal("Outstanding", action.Group);
        Assert.Equal("Yêu cầu chốt công thức", action.Title.Vi);
        Assert.Equal("Delivery", action.Form); // Optional note, matching the live chip request.
        Assert.Equal(new[] { "note" }, action.Fields);
        var requested = Act(view, action.Id, new());
        Assert.Equal("FinalizeChip", requested.WorkCode);
        Assert.Equal("Lab", requested.WorkRole);
        Assert.Equal("Open", requested.State.Status);
    }
    [Theory]
    [InlineData("quote-new", false)]
    [InlineData("quote-price-accepted", false)]
    [InlineData("quote-finalize", false)]
    [InlineData("quote-order-ready", false)]
    public void QuoteCompletionIsHiddenUntilLabFinalizes(string id, bool visible)
    {
        var view = Read(id);
        Assert.Equal(visible, view.Actions.Any(a => a.Id == "Complete"));
        if (visible) Assert.True(Assert.Single(view.Actions, a => a.Id == "Complete").Enabled);
        else Assert.Throws<ArgumentException>(() => Act(view, "Complete"));
    }
    [Fact]
    public void DeliveryNoteDoesNotOverwriteTheFormulaNote()
    {
        var initial = Read("sample-price-accepted", "Lab");
        initial = Read(initial.ScenarioId, "Lab", initial.State with { Workflow = initial.State.Workflow! with { FormulaName = "F001", FormulaNote = "Formula note" } });
        var sent = Act(initial, "SendKg", new() { Date = "2026-11-15", Kg = 3, Note = "Short delivery note" });
        Assert.Equal("Formula note", sent.State.Workflow!.FormulaNote);
        Assert.Equal("Short delivery note", Assert.Single(sent.State.History).Note);
    }
    [Theory]
    [InlineData("chip-new", "RequestChip", 1, 10)]
    [InlineData("sample-new", "RequestSample", 1, 10)]
    [InlineData("both-new", "RequestChipThenSample", 1, 10)]
    [InlineData("both-approved", "RequestChipThenSample", 1, 10)]
    public void IdenticalPendingPlanCannotAppendEventsOrHistory(string id, string action, int sets, decimal kg)
    {
        var initial = Read(id); var events = initial.State.Workflow!.Events.ToArray();
        Assert.NotNull(initial.PendingDeliveryPlan);
        var error = Assert.Throws<ArgumentException>(() => Act(initial, action, new() { Sets = sets, Kg = kg, Note = "Only the note changed" }));
        Assert.Equal("SimulationPlanUnchanged", error.Message);
        Assert.Equal(events, initial.State.Workflow.Events); Assert.Empty(initial.State.History);
        var changed = Act(initial, action, new() { Sets = sets + 1, Kg = kg + 1 });
        Assert.Single(changed.State.History); Assert.Equal(events.Length + 1, changed.State.Workflow!.Events.Count);
    }
    [Fact]
    public void SamePlanAfterDeliveryIsStillANewSupplementaryRequest()
    {
        var initial = Read("sample-completed"); Assert.Null(initial.PendingDeliveryPlan);
        var next = Act(initial, "RequestSample", new() { Kg = 10 });
        Assert.Single(next.State.History); Assert.Equal("Open", next.State.Status);
    }
    [Theory]
    [InlineData("sample-quoted")]
    [InlineData("chip-quoted")]
    [InlineData("both-quoted")]
    public void SaleSeesPendingPriceResponseWhileLabPreparesDelivery(string id)
    {
        var sale = Read(id); Assert.Equal("Lab", sale.WorkRole);
        Assert.Equal("PriceFeedback", sale.PrimaryAction?.Id);
        Assert.NotEmpty(sale.PendingLab); Assert.NotEmpty(sale.PendingSale);
        var lab = Read(id, "Lab"); Assert.NotEmpty(lab.PendingSale);
        Assert.DoesNotContain(lab.Actions, a => a.Id == "PriceFeedback");
        var accepted = Act(sale, "PriceFeedback", new() { QuotationFeedback = new("2026-11-10", "Accepted", "2026-11-15", "", "", "Accepted price", "") });
        Assert.Null(accepted.PrimaryAction); Assert.Equal("Lab", accepted.WorkRole); Assert.Empty(accepted.PendingSale);
    }
    [Fact]
    public void SaleSeesBothFeedbackTasksAndPendingChipResponseAlongsideKgDelivery()
    {
        var both = Read("both-sent-price-waiting");
        Assert.Contains(both.Actions, a => a.Id == "ChipFeedback" && a.Group == "Outstanding");
        Assert.Contains(both.Actions, a => a.Id == "PriceFeedback" && a.Group == "Outstanding");
        Assert.Equal(2, both.PendingSale.Count); Assert.NotNull(both.PrimaryAction);
        Assert.Equal("ChipFeedback", Read("both-sent").PrimaryAction?.Id);
    }
    public static IEnumerable<object[]> Cases() => SimulationCatalog.All().SelectMany(s => new[] { "Sale", "Lab" }.Select(role => new object[] { s.Id, role }));
    private static SimulationView Read(string id, string role = "Sale", SimulationState? state = null) => SimulationTransitions.Evaluate(new() { ScenarioId = id, Audience = role, State = state });
    private static SimulationView Act(SimulationView view, string id, SimulationInput? input = null, string? role = null) => SimulationTransitions.Evaluate(new() {
        ScenarioId = view.ScenarioId, Audience = role ?? view.Audience, State = view.State, ActionId = id, Input = input ?? new() });
    [Fact]
    public void CatalogContainsOnlyOrderedBusinessCasesAndKeepsTechnicalFixturesOutOfPicker()
    {
        var catalog = SimulationCatalog.All();
        Assert.Equal(new[] { "Quotation", "Chip", "Sample", "ChipThenSample" }, catalog.Select(s => s.Group).Distinct());
        Assert.Equal(catalog.Count, catalog.Select(s => s.Id).Distinct().Count());
        Assert.DoesNotContain(catalog, s => s.Id is "price-readonly" or "price-date-only" or "old-batches");
        foreach (var group in catalog.GroupBy(s => s.Group)) {
            Assert.EndsWith("-new", group.First().Id);
            Assert.Contains(group, s => s.Id.EndsWith("-completed")); Assert.Contains(group, s => s.Id.EndsWith("-cancelled"));
            Assert.Contains(group, s => s.Id.EndsWith("-price-rejected")); Assert.Contains(group, s => s.Id.EndsWith("-price-accepted"));
        }
    }
    [Fact]
    public void SupplementaryPlansAreSeparatedFromCurrentBusinessDecisions()
    {
        Assert.All(Read("quote-completed").Actions, a => Assert.Equal("Supplementary", a.Group));
        var accepted = Read("quote-price-accepted");
        Assert.Equal("Other", accepted.Actions.Single(a => a.Id == "RequestChip").Group);
        Assert.Equal("Supplementary", accepted.Actions.Single(a => a.Id == "RequestRequote").Group);
        Assert.Equal("Supplementary", Read("quote-new").Actions.Single(a => a.Id == "RequestChip").Group);
    }
    [Theory, MemberData(nameof(Cases))]
    public void SaleCanCancelOnlyOpenRequestsWithAnAuditedReason(string id, string role)
    {
        var view = Read(id, role);
        if (role != "Sale" || view.State.Status is "Cancelled" or "Completed") {
            Assert.DoesNotContain(view.Actions, action => action.Id == "CancelRejectedRequest"); return;
        }
        var cancel = Assert.Single(view.Actions, action => action.Id == "CancelRejectedRequest");
        Assert.Equal("Supplementary", cancel.Group); Assert.True(cancel.Danger);
        Assert.Throws<ArgumentException>(() => Act(view, cancel.Id));
        var result = Act(view, cancel.Id, new() { Note = "Customer cancelled this request" });
        Assert.Equal("Cancelled", result.State.Status);
        Assert.Equal("Customer cancelled this request", Assert.Single(result.State.History).Note);
        Assert.Empty(result.Actions); Assert.Empty(Read(id, "Lab", result.State).Actions);
    }
    [Theory, MemberData(nameof(Cases))]
    public void BothAudiencesUseProductionAssignmentAndCannotInvokeOtherRolesActions(string id, string role)
    {
        var view = Read(id, role);
        Assert.NotNull(view.Stage); Assert.NotNull(view.WorkRole);
        Assert.Equal(view.Actions.Count, view.Actions.Select(a => a.Id).Distinct().Count());
        if (view.PrimaryAction is not null) Assert.Contains(view.PrimaryAction, view.Actions);
        if (role == "Sale") Assert.DoesNotContain(view.Actions, a => a.Id is "PrepareFormula" or "SendChip" or "SendKg" or "FinalizeChip" or "FinalizeQuotation");
        else Assert.DoesNotContain(view.Actions, a => a.Id is "PriceFeedback" or "ChipFeedback" or "SampleFeedback" or "Complete" || a.Id.StartsWith("Request"));
        Assert.DoesNotContain(view.Actions, a => a.Id == "ApproveStandardPrice");
        if (view.State.Status == "Cancelled") { Assert.Empty(view.Actions); Assert.Empty(view.ExternalActions); }
        foreach (var forbidden in role == "Lab" ? new[] { "Complete", "PriceFeedback" } : new[] { "PrepareFormula", "FinalizeQuotation", "SendChip" })
            Assert.Throws<ArgumentException>(() => Act(view, forbidden));
    }
    [Theory]
    [InlineData("quote-new", "ApproveFormula", "Lab")]
    [InlineData("quote-confirmed", "ApproveStandardPrice", "President")]
    [InlineData("quote-priced", "SendQuotation", "Sale")]
    [InlineData("quote-quoted", "PriceFeedback", "Sale")]
    [InlineData("quote-price-accepted", "ChooseAfterPrice", "Sale")]
    [InlineData("quote-price-rejected", "ChooseRejectedPriceAction", "Sale")]
    [InlineData("quote-price-follow-up", "TrackRejectedPrice", "Sale")]
    [InlineData("quote-price-rework", "AdjustFormulaPrice", "Lab")]
    [InlineData("quote-price-reconfirm", "ApproveStandardPrice", "President")]
    [InlineData("quote-finalize", "FinalizeQuotation", "Lab")]
    [InlineData("chip-sent", "FollowUpCustomer", "Sale")]
    [InlineData("chip-failed", "ReworkFormula", "Lab")]
    [InlineData("chip-approved", "ChooseAfterChip", "Sale")]
    [InlineData("chip-finalize", "FinalizeChip", "Lab")]
    [InlineData("sample-approved", "CompleteRequest", "Sale")]
    [InlineData("sample-sample-price-pending", "PriceFeedback", "Sale")]
    [InlineData("both-sent", "SendSample", "Lab")]
    [InlineData("both-sample-approved", "CompleteRequest", "Sale")]
    public void BusinessMilestonesMatchLiveWorkRouting(string id, string code, string role)
    {
        var view = Read(id); Assert.Equal(code, view.WorkCode); Assert.Equal(role, view.WorkRole);
    }
    [Fact]
    public void QuotationJourneySeparatesPriceAcceptanceLabFinalizationAndSaleCompletion()
    {
        var v = Read("quote-new", "Lab"); v = Act(v, "PrepareFormula");
        Assert.Equal("President", v.WorkRole); Assert.DoesNotContain(v.Actions, a => a.Group == "Outstanding"); Assert.Single(v.ExternalActions);
        v = Act(v, "ApproveStandardPrice"); Assert.Equal("SendQuotation", v.WorkCode);
        v = Act(v, "PriceFeedback", new() { QuotationFeedback = new("2026-11-10", "Accepted", "2026-11-15", "", "", "Customer agreed", "") }, "Sale");
        Assert.Equal("ChooseAfterPrice", v.WorkCode); Assert.DoesNotContain(v.Actions, a => a.Id == "Complete");
        Assert.Equal("Outstanding", Assert.Single(v.Actions, a => a.Id == "RequestLabFinalization").Group);
        v = Act(v, "RequestLabFinalization", new() { Note = "Finalize for ordering" });
        Assert.Equal("FinalizeQuotation", v.WorkCode);
        v = Act(v, "FinalizeQuotation", role: "Lab");
        Assert.Equal("Completed", v.State.Status);
        Assert.DoesNotContain(v.Actions, a => a.Id == "Complete");
        v = Read(v.ScenarioId, "Sale", v.State);
        Assert.Contains(v.Actions, a => a.Id == "RequestSample"); Assert.Equal(5, v.State.History.Count);
    }
    [Fact]
    public void ChipThenSampleAllowsParallelChipFeedbackAndAcceptedKgAutoCompletes()
    {
        var v = Read("both-sent", "Lab"); Assert.Contains(v.Actions, a => a.Id == "SendKg");
        var sale = Read(v.ScenarioId, "Sale", v.State); Assert.Contains(sale.Actions, a => a.Id == "ChipFeedback");
        v = Act(v, "SendKg", new() { Date = "2026-11-15", Kg = 7 });
        Assert.Equal("FollowUpCustomer", v.WorkCode); Assert.Equal(7, v.State.Batches.Single().Options.Single().Kg);
        v = Act(v, "SampleFeedback", new() { Result = "Approved", Date = "2026-11-15", Note = "Accepted" }, "Sale");
        Assert.Equal("Completed", v.State.Status);
        Assert.Equal("Completed", v.State.Workflow!.FormulaStatus);
        Assert.DoesNotContain(v.Actions, a => a.Id == "Complete");
    }
    [Theory]
    [InlineData("chip-sent", "ChipFeedback")]
    [InlineData("sample-sent", "SampleFeedback")]
    public void FailedFeedbackRoutesToLabAndCannotBeCompleted(string id, string feedback)
    {
        var v = Read(id);
        Assert.Throws<ArgumentException>(() => Act(v, feedback, new() { Date = "2026-11-15", Result = "Failed" }));
        v = Act(v, feedback, new() { Date = "2026-11-15", Result = "Failed", Note = "Too light" });
        Assert.Equal("ReworkFormula", v.WorkCode); Assert.Equal("Lab", v.WorkRole);
        Assert.Throws<ArgumentException>(() => Act(v, "Complete"));
    }
    [Fact]
    public void CancelRequiresReasonAndClosedRequestHasNoFurtherActions()
    {
        var v = Read("quote-price-rejected"); Assert.Throws<ArgumentException>(() => Act(v, "CancelRejectedRequest"));
        v = Act(v, "CancelRejectedRequest", new() { Note = "Customer cancelled" });
        Assert.Equal("Cancelled", v.State.Status); Assert.Empty(v.Actions); Assert.Single(v.State.History);
    }
    [Fact]
    public void FailedSampleKeepsRetryAndReasonedCancellationAvailable()
    {
        var failed = Act(Read("sample-sent"), "SampleFeedback",
            new() { Date = "2026-11-15", Result = "Failed", Note = "Too light" });
        Assert.Equal("Open", failed.State.Status);
        Assert.Equal("Rework", failed.State.Workflow!.FormulaStatus);
        Assert.Contains(failed.Actions, a => a.Id == "RequestSample" && a.Enabled);
        Assert.Contains(failed.Actions, a => a.Id == "CancelRejectedRequest" && a.Enabled);
        Assert.Throws<ArgumentException>(() => Act(failed, "CancelRejectedRequest"));
        var cancelled = Act(failed, "CancelRejectedRequest", new() { Note = "Customer stopped testing" });
        Assert.Equal("Cancelled", cancelled.State.Status);
        var retry = Act(failed, "RequestSample", new() { Kg = 5, Note = "Make a darker sample" });
        Assert.Equal("Open", retry.State.Status);
        Assert.Equal("Lab", retry.WorkRole);
        Assert.NotEqual("Completed", retry.State.Workflow!.FormulaStatus);
    }
    [Theory]
    [InlineData("chip-sent", "ChipFeedback")]
    [InlineData("sample-sent", "SampleFeedback")]
    public void CustomerCancellationClosesTheRequest(string scenario, string action)
    {
        var v = Act(Read(scenario), action, new() { Result = "Cancelled", Date = "2026-11-15", Note = "Customer stopped the trial" });
        Assert.Equal("Cancelled", v.State.Status); Assert.Equal("Cancelled", v.State.Workflow!.TrialResult);
        Assert.Empty(Read(v.ScenarioId, "Lab", v.State).Actions); Assert.Empty(v.Actions);
    }
    [Fact]
    public void RequoteNotificationDoesNotInvalidatePriceOrReopenCompletedWork()
    {
        var v = Read("quote-completed"); var previous = v.State;
        v = Act(v, "RequestRequote", new() { Note = "Please quote again" });
        Assert.Equal(previous.Status, v.State.Status); Assert.Equal(previous.Workflow, v.State.Workflow);
        Assert.True(v.State.PriceAccepted); Assert.Single(v.State.History);
    }
    [Fact]
    public void FailedKgRetriesExistingFormulaUnderProductionRetryRule()
    {
        var v = Read("sample-failed", "Lab");
        var originalStarts = v.State.Workflow!.FormulaStarts.Count;
        v = Act(v, "PrepareFormula"); Assert.Equal(originalStarts, v.State.Workflow!.FormulaStarts.Count);
        Assert.Contains(v.Actions, a => a.Id == "SendKg");
        v = Act(v, "SendKg", new() { Date = "2026-11-15", Kg = 2 });
        v = Act(v, "SampleFeedback", new() { Date = "2026-11-15", Result = "Approved", Note = "Accepted new formula" }, "Sale");
        Assert.Equal("Completed", v.State.Status);
        Assert.DoesNotContain(v.Actions, a => a.Id == "Complete");
        Assert.Equal(originalStarts, v.State.Workflow!.FormulaStarts.Count);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryAvailableChoiceReturnsAUsableNextViewForBothAudiences(string id, string role)
    {
        var initial = Read(id, role);
        foreach (var action in initial.Actions.Concat(initial.ExternalActions).Where(a => a.Enabled)) {
            var after = Act(initial, action.Id, new() { Date = "2026-11-15", Sets = 2, Kg = 5, Note = "Customer requested this next step", Result = "Approved",
                QuotationFeedback = new("2026-11-10", "Accepted", "2026-11-15", "", "", "Customer accepted price", "") });
            Assert.NotNull(after.Stage); Assert.Single(after.State.History);
            var other = Read(id, role == "Sale" ? "Lab" : "Sale", after.State);
            Assert.Equal(after.WorkCode, other.WorkCode); Assert.Equal(after.WorkRole, other.WorkRole);
        }
    }
}
