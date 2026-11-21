using System.Globalization;

namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

public static class SimulationTransitions
{
    public static SimulationView Evaluate(SimulationStepRequest request)
    {
        var scenario = SimulationCatalog.Find(request.ScenarioId)
            ?? throw new ArgumentException("UnknownSimulationScenario");
        if (request.Audience is not ("Lab" or "Sale") || request.Input is null) throw new ArgumentException("InvalidSimulationInput");
        var state = request.State ?? scenario.State;
        Validate(state);
        if (scenario.State.Workflow is not null) return BusinessSimulation.Evaluate(request, scenario, state);
        var view = SimulationDecisions.Build(scenario.Id, request.Audience, state);
        if (string.IsNullOrEmpty(request.ActionId)) return view;
        var action = view.Actions.SingleOrDefault(a => a.Id == request.ActionId) ?? throw new ArgumentException("SimulationActionUnavailable");
        if (!action.Enabled) throw new ArgumentException("SimulationActionUnavailable");
        var fromStatus = state.Status;
        if (state.Status == "Completed" && action.Id.StartsWith("Request")) state = ArchiveCompleted(state);
        var input = request.Input;
        if ((input.Note?.Length ?? 0) > 2000 || input.Options is null || input.Options.Count > 30 || input.Feedbacks is null || input.Feedbacks.Count > 30) throw new ArgumentException("InvalidSimulationInput");
        var note = input.Note?.Trim() ?? "";
        if (action.Fields.Contains("sets") && input.Sets is not (>= 1 and <= 30)) throw new ArgumentException("InvalidSimulationSets");
        if (action.Fields.Contains("kg") && input.Kg is not (> 0 and <= 999999999)) throw new ArgumentException("InvalidSimulationKg");
        if (action.Form == "Note" && action.Id != "UpdateNote" && note.Length == 0) throw new ArgumentException("SimulationNoteRequired");
        if (action.Fields.Contains("date") && !ValidDate(input.Date)) throw new ArgumentException("InvalidSimulationDate");
        state = action.Id switch {
            "RequestChip" when state.SampleKg > 0 => throw new ArgumentException("SimulationChipSequenceUnresolved"),
            "RequestChip" => state with { ChipSets = input.Sets!.Value, ChipRequired = true, ChipResult = "", ChipFeedbackDone = false, Note = note },
            "RequestKg" => state with { SampleKg = input.Kg!.Value, SampleRequired = true, Note = note },
            "RequestCombined" => state with { ChipSets = input.Sets!.Value, SampleKg = input.Kg!.Value, ChipRequired = true, SampleRequired = true, ChipResult = "", ChipFeedbackDone = false, Combined = true, Note = note },
            "EditPending" => state with { ChipSets = state.ChipSets > 0 ? input.Sets!.Value : 0, SampleKg = state.SampleKg > 0 ? input.Kg!.Value : 0, Note = note },
            "UpdateNote" => state with { Note = note },
            "RequestQuote" or "LowerPrice" => state with { QuotePending = true, PriceAccepted = false, PricePending = false, PriceRejected = false, Note = note, QuotationFeedback = null },
            "PrepareFormula" => state with { FormulaReady = true },
            "PrepareQuote" => state with { QuotePending = false, QuoteReady = true },
            "SendQuote" => state with { QuoteReady = false, PricePending = true, PriceRequired = true },
            "SendChip" => state with { ChipSets = 0, ChipRequired = true, ChipSent = true, ChipResult = "", ChipFeedbackDone = false, HasHistory = true },
            "SendKg" => SendOptions(state, input),
            "PriceFeedback" or "ViewPriceFeedback" => PriceFeedback(state, input, SimulationCatalog.Quotation(scenario.Id, state)),
            "ChipFeedback" => ChipFeedback(state, input),
            "Complete" => state with { Status = "Completed" },
            "Cancel" => state with { Status = "Cancelled", Note = note },
            "Reject" => state with { Status = "Rejected", RejectionReason = note, RejectedFromStatus = state.Status },
            "Resume" => state with { Status = state.RejectedFromStatus == "Completed" ? "Completed" : "Open", Note = note.Length > 0 ? note : state.Note },
            "Supplement" => ArchiveCompleted(state),
            _ when action.Form == "BatchFeedback" => BatchFeedback(state, action.BatchNumber!.Value, input),
            _ => throw new ArgumentException("SimulationActionUnavailable")
        };
        state = state with { History = state.History.Append(new SimulationHistory(action.Id, note, fromStatus, state.Status, action.Title)).ToArray() };
        Validate(state);
        return SimulationDecisions.Build(scenario.Id, request.Audience, state);
    }
    private static SimulationState ArchiveCompleted(SimulationState state) => state with {
        Status = "Open", ArchivedBatches = state.ArchivedBatches.Concat(state.Batches).ToArray(), Batches = [],
        ChipRequired = false, SampleRequired = false, ChipSent = false, ChipFeedbackDone = false, ChipResult = "", Combined = false,
        ChipSets = 0, SampleKg = 0, UnresolvedRule = ""
    };

    private static SimulationState PriceFeedback(SimulationState state, SimulationInput input, SimulationQuotationContext context)
    {
        if (input.Result is not ("Approved" or "Failed" or "Waiting")) throw new ArgumentException("InvalidSimulationResult");
        var flowAllows = context.AllowedActions.Any(a => a is "PriceRejected" or "PriceNoResponse") || context.PrimaryActionCode == "SendQuotation" && context.IsActionForCurrentUser;
        var canEditDate = context.DateEditable || flowAllows;
        var canAnswer = !state.PriceAccepted && (context.CanConfirmCustomerPrice || flowAllows);
        if (input.QuotationFeedback is { } feedback)
        {
            ValidateQuotationFeedback(feedback);
            var expected = feedback.Outcome == "Accepted" ? "Approved" : feedback.Outcome == "Rejected" ? "Failed" : "Waiting";
            if (input.Result != expected) throw new ArgumentException("InvalidSimulationResult");
            var requoted = context.PreviousOutcome == "Rejected" && context.ApprovedAt is not null && string.CompareOrdinal(context.ApprovedAt, context.PreviousDate) > 0;
            var outcome = state.PriceAccepted ? "Accepted" : context.PreviousOutcome == "Rejected" && !requoted ? "Rejected" : "NoResponse";
            var baselineNote = requoted || context.NoteHidden ? "" : context.PreviousNote ?? "";
            var reference = context.PriceReferences.FirstOrDefault();
            var recordsFeedback = feedback.Outcome != outcome || feedback.Note != baselineNote || feedback.PrivateNote != "" ||
                feedback.Reference != (reference?.Label ?? (!context.ContentHidden ? context.PriceReference : null) ?? "") ||
                feedback.QuotationLineId != (reference?.QuotationLineId ?? "") ||
                outcome != "NoResponse" && feedback.FeedbackDate != (state.PriceAccepted ? context.AcceptedAt : context.PreviousDate);
            if ((context.QuotedAt is null || feedback.Date != context.QuotedAt) && !canEditDate || recordsFeedback && !canAnswer)
                throw new ArgumentException("SimulationActionUnavailable");
            if (feedback.QuotationLineId.Length > 0 && !context.PriceReferences.Any(r => r.QuotationLineId == feedback.QuotationLineId))
                throw new ArgumentException("InvalidSimulationFeedback");
            if (recordsFeedback && string.IsNullOrWhiteSpace(feedback.Note)) throw new ArgumentException("SimulationNoteRequired");
            state = state with { QuotationFeedback = feedback };
        }
        else if (!canAnswer) throw new ArgumentException("SimulationActionUnavailable");
        return input.Result == "Waiting" ? state : state with { PricePending = false, PriceAccepted = input.Result == "Approved", PriceRejected = input.Result == "Failed" };
    }
    internal static void ValidateQuotationFeedback(SimulationQuotationFeedback feedback)
    {
        if (!ValidDate(feedback.Date) || !ValidDate(feedback.FeedbackDate) ||
            feedback.Outcome is not ("NoResponse" or "Accepted" or "Rejected") ||
            feedback.Outcome != "NoResponse" && string.CompareOrdinal(feedback.FeedbackDate, feedback.Date) < 0 ||
            feedback.Note is null || feedback.Note.Length > 2000 || feedback.PrivateNote is null || feedback.PrivateNote.Length > 2000 ||
            feedback.Reference is null || feedback.Reference.Length > 500 || feedback.QuotationLineId is null || feedback.QuotationLineId.Length > 100)
            throw new ArgumentException("InvalidSimulationFeedback");
    }
    private static SimulationState ChipFeedback(SimulationState state, SimulationInput input)
    {
        if (input.Result is not ("Approved" or "Failed" or "Waiting")) throw new ArgumentException("InvalidSimulationResult");
        return state with { ChipFeedbackDone = input.Result != "Waiting", ChipResult = input.Result == "Waiting" ? "" : input.Result! };
    }
    private static SimulationState SendOptions(SimulationState state, SimulationInput input)
    {
        var candidates = SimulationCatalog.Candidates();
        if (input.Options.Count is < 1 or > 30 || input.Options.Any(o => o is null || !candidates.Any(c => c.Id == o.Id) || o.Kg is <= 0 or > 999999999) ||
            input.Options.Select(o => o.Id).Distinct().Count() != input.Options.Count) throw new ArgumentException("InvalidSimulationOptions");
        if (!candidates.Single(c => c.Id == input.Options[0].Id).Confirmed) throw new ArgumentException("SimulationConfirmedFirstRequired");
        var number = state.Batches.Concat(state.ArchivedBatches).Select(b => b.Number).DefaultIfEmpty().Max() + 1;
        var batch = new SimulationBatch(number, input.Date!, input.Options.Select(o => new SimulationOption($"option-{number}-{o.Id}", candidates.Single(c => c.Id == o.Id).Code, o.Kg, Confirmed: candidates.Single(c => c.Id == o.Id).Confirmed)).ToArray());
        return state with { Batches = state.Batches.Append(batch).ToArray(), SampleKg = 0, SampleRequired = true, HasHistory = true,
            UnresolvedRule = state.Batches.Any(b => b.Options.All(o => o.Result == "")) ? "OldBatchPolicy" : state.UnresolvedRule };
    }
    private static SimulationState BatchFeedback(SimulationState state, int batchNumber, SimulationInput input)
    {
        var batch = state.Batches.Single(b => b.Number == batchNumber);
        var changes = input.Feedbacks.Count > 0 ? input.Feedbacks : [new SimulationFeedbackInput(input.OptionId ?? "", input.Result ?? "", input.Date ?? "", input.Note ?? "")];
        if (changes.Any(f => f is null || f.Result is not ("Approved" or "Failed") || !batch.Options.Any(o => o.Id == f.OptionId) || !ValidDate(f.Date) ||
            string.CompareOrdinal(f.Date, batch.SentDate) < 0 || f.Note is null || f.Note.Length > 2000) ||
            changes.Select(f => f.OptionId).Distinct().Count() != changes.Count || changes.Count(f => f.Result == "Approved") > 1)
            throw new ArgumentException("InvalidSimulationFeedback");
        var winner = changes.SingleOrDefault(f => f.Result == "Approved");
        var selectedIndex = winner is null ? 0 : batch.Options.Select((o, i) => (o, i)).Single(x => x.o.Id == winner.OptionId).i + 1;
        var options = batch.Options.Select(o => {
            var change = changes.SingleOrDefault(f => f.OptionId == o.Id);
            var updated = change is null ? o : o with { Result = change.Result, Note = change.Note.Trim(), ReplyDate = change.Date };
            return winner is not null && o.Id != winner.OptionId ? updated with { Result = "Failed", Note = change?.Note.Trim() ?? $"Không đạt do khách chọn Option {selectedIndex} / Not selected: Option {selectedIndex}", ReplyDate = winner.Date } : updated;
        }).ToArray();
        var batches = state.Batches.Select(b => b.Number == batchNumber ? b with { Options = options } : b).ToArray();
        return state with { Batches = batches, Status = winner is not null ? "Completed" : state.Status,
            UnresolvedRule = state.UnresolvedRule == "OldBatchPolicy" && batches.All(b => b.Options.Any(o => o.Result != "")) ? "" : state.UnresolvedRule };
    }
    private static bool ValidDate(string? date) => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    private static void Validate(SimulationState s)
    {
        if (s.QuotationFeedback is { } feedback) ValidateQuotationFeedback(feedback);
        if (s.Status is not ("Open" or "Completed" or "Cancelled" or "LabDeclined" or "Rejected" or "Review") || s.ChipSets is < 0 or > 30 || s.SampleKg is < 0 or > 999999999 ||
            s.ChipResult is not ("" or "Approved" or "Failed" or "Cancelled") || s.RejectionReason is null || s.RejectionReason.Length > 2000 ||
            s.RejectedFromStatus is not ("Open" or "Completed" or "Cancelled") || s.Notifications is null || s.Notifications.Count > 2000 ||
            s.Notifications.Any(n => n is null || n.Group != "General" || n.Actor is not ("Lab" or "Sale") || n.Reason is null || n.Reason.Length > 2000 ||
                n.Title is null || n.Title.Vi is null || n.Title.En is null || n.Title.Vi.Length > 300 || n.Title.En.Length > 300) || s.History is null || s.History.Count > 2000 ||
            s.History.Any(h => h is null || h.Note is null || h.Note.Length > 2000 || h.ActionId is null || h.ActionId.Length > 100 || h.FromStatus is null || h.FromStatus.Length > 30 || h.ToStatus is null || h.ToStatus.Length > 30 ||
                h.Title is not null && (h.Title.Vi is null || h.Title.Vi.Length > 300 || h.Title.En is null || h.Title.En.Length > 300)) ||
            s.Note is null || s.Note.Length > 2000 || s.UnresolvedRule is not ("" or "OldBatchPolicy" or "LegacyEvidence") ||
            s.Batches is null || s.ArchivedBatches is null || s.Batches.Count + s.ArchivedBatches.Count > 20)
            throw new ArgumentException("InvalidSimulationState");
        var batches = s.Batches.Concat(s.ArchivedBatches).ToArray();
        if (batches.Any(b => b is null || b.Number is < 1 or > 1000 || !ValidDate(b.SentDate) || b.Options is null || b.Options.Count is < 1 or > 30 ||
            b.Options.Any(o => o is null || string.IsNullOrWhiteSpace(o.Id) || o.Id.Length > 80 || string.IsNullOrWhiteSpace(o.Code) || o.Code.Length > 80 ||
                o.Kg is <= 0 or > 999999999 || o.Result is not ("" or "Approved" or "Failed" or "Cancelled") || o.Note is null || o.Note.Length > 2000 ||
                o.ReplyDate is not null && !ValidDate(o.ReplyDate)) || b.Options.Select(o => o.Id).Distinct().Count() != b.Options.Count || b.Options.Count(o => o.Result == "Approved") > 1) ||
            batches.Select(b => b.Number).Distinct().Count() != batches.Length) throw new ArgumentException("InvalidSimulationState");
    }
}
