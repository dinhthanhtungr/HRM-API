namespace HRM.Application.Features.Development.SampleWorkflowSimulation;

public sealed record SimulationText(string Vi, string En);
public sealed record SimulationOption(string Id, string Code, decimal Kg, string Result = "", string Note = "", string? ReplyDate = null, bool Confirmed = true);
public sealed record SimulationFeedbackInput(string OptionId, string Result, string Date, string Note);
public sealed record SimulationHistory(string ActionId, string Note, string FromStatus, string ToStatus, SimulationText? Title = null);
public sealed record SimulationBatch(int Number, string SentDate, IReadOnlyList<SimulationOption> Options);
public sealed record SimulationNotification(string Group, string Actor, SimulationText Title, string Reason);
public sealed record SimulationState
{
    public SimulationWorkflow? Workflow { get; init; }
    public SimulationQuotationFeedback? QuotationFeedback { get; init; }
    public int ChipSets { get; init; }
    public decimal SampleKg { get; init; }
    public bool QuotePending { get; init; }
    public bool QuoteReady { get; init; }
    public bool FormulaReady { get; init; } = true;
    public bool PricePending { get; init; }
    public bool PriceAccepted { get; init; }
    public bool PriceRejected { get; init; }
    public bool PriceRequired { get; init; } = true;
    public bool Combined { get; init; }
    public bool ChipSent { get; init; }
    public bool ChipFeedbackDone { get; init; }
    public string ChipResult { get; init; } = "";
    public bool ChipRequired { get; init; }
    public bool SampleRequired { get; init; }
    public string RejectionReason { get; init; } = "";
    public string RejectedFromStatus { get; init; } = "Open";
    public IReadOnlyList<SimulationHistory> History { get; init; } = [];
    public IReadOnlyList<SimulationNotification> Notifications { get; init; } = [];
    public bool HasHistory { get; init; }
    public string Status { get; init; } = "Open";
    public string Note { get; init; } = "";
    public string UnresolvedRule { get; init; } = "";
    public IReadOnlyList<SimulationBatch> Batches { get; init; } = [];
    public IReadOnlyList<SimulationBatch> ArchivedBatches { get; init; } = [];
}
public sealed record SimulationScenario(string Id, SimulationText Title, SimulationState State)
{
    public string Group { get; init; } = "";
}
public sealed record SimulationPendingPlan(string Plan, int Sets, decimal Kg);
// Only synthetic evidence is transported. No production IDs or mutations are accepted.
public sealed record SimulationWorkflow
{
    public string FormulaName { get; init; } = "F001";
    public string FormulaNote { get; init; } = "";
    public IReadOnlyList<int> FormulaStarts { get; init; } = [0];
    public int RequestedSets { get; init; } = 1;
    public decimal RequestedKg { get; init; } = 10;
    public decimal DeliveredKg { get; init; } = 10;
    public string SentDate { get; init; } = "2026-11-14";
    public string? ReplyDate { get; init; }
    public string Plan { get; init; } = "Quotation";
    public string FormulaStatus { get; init; } = "Draft";
    public bool StandardPriceApproved { get; init; }
    public string? QuotedAt { get; init; }
    public string TrialKind { get; init; } = "";
    public string TrialResult { get; init; } = "";
    public int TrialNo { get; init; }
    public IReadOnlyList<string> Events { get; init; } = [];
}
public sealed record SimulationAction(string Id, SimulationText Title, SimulationText Description, string Form,
    IReadOnlyList<string> Fields, int? BatchNumber = null, bool Danger = false)
{
    public string Group { get; init; } = "Other";
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<SimulationText> DisabledReasons { get; init; } = [];
}
public sealed record SimulationView(string ScenarioId, string Audience, SimulationState State,
    IReadOnlyList<SimulationText> PendingLab, IReadOnlyList<SimulationText> PendingSale,
    IReadOnlyList<SimulationAction> Actions, SimulationAction? PrimaryAction,
    IReadOnlyList<SimulationText> CompletionBlockers, SimulationText? Warning)
{
    public string? WorkCode { get; init; }
    public SimulationPendingPlan? PendingDeliveryPlan { get; init; }
    public string? WorkRole { get; init; }
    public string? Stage { get; init; }
    public IReadOnlyList<SimulationAction> ExternalActions { get; init; } = [];
    public string RequestCode { get; init; } = "TP_SIM_31074";
    public string ProductCode { get; init; } = "WP31070C";
    public SimulationText ProductName { get; init; } = new("HẠT TÁI SINH PP MÀU TRẮNG 100% PCR", "WHITE RECYCLED PP 100% PCR");
    public IReadOnlyList<SimulationOption> Candidates { get; init; } = SimulationCatalog.Candidates();
    public SimulationQuotationContext Quotation { get; init; } = new();
}
public sealed record SimulationInput
{
    public SimulationQuotationFeedback? QuotationFeedback { get; init; }
    public int? Sets { get; init; }
    public decimal? Kg { get; init; }
    public string? Note { get; init; }
    public string? Date { get; init; }
    public string? Result { get; init; }
    public string? OptionId { get; init; }
    public IReadOnlyList<SimulationOption> Options { get; init; } = [];
    public IReadOnlyList<SimulationFeedbackInput> Feedbacks { get; init; } = [];
}
public sealed record SimulationStepRequest
{
    public string ScenarioId { get; init; } = "quote-new";
    public string Audience { get; init; } = "Sale";
    public SimulationState? State { get; init; }
    public string? ActionId { get; init; }
    public SimulationInput Input { get; init; } = new();
}
public sealed record SimulationQuotationFeedback(string Date, string Outcome, string FeedbackDate,
    string Reference, string QuotationLineId, string Note, string PrivateNote);
public sealed record SimulationQuotationReference(string QuotationLineId, string Label, string? SentAt);
public sealed record SimulationQuotationContext
{
    public bool DateEditable { get; init; } = true;
    public bool CanConfirmCustomerPrice { get; init; } = true;
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public string? PrimaryActionCode { get; init; }
    public bool IsActionForCurrentUser { get; init; }
    public string? QuotedAt { get; init; }
    public string? AcceptedAt { get; init; }
    public bool ContentHidden { get; init; }
    public bool NoteHidden { get; init; }
    public string? ApprovedAt { get; init; }
    public string? PreviousOutcome { get; init; }
    public string? PreviousDate { get; init; }
    public string? PreviousNote { get; init; }
    public string? PriceReference { get; init; }
    public IReadOnlyList<SimulationQuotationReference> PriceReferences { get; init; } = [];
}
