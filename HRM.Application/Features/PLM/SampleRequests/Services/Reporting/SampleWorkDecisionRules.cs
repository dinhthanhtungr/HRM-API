using System.Linq.Expressions;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal sealed record SampleWorkFacts
{
    public bool InternalDirect { get; init; }
    public bool Open { get; init; }
    public bool LabPaused { get; init; }
    public bool Legacy { get; init; }
    public bool Sent { get; init; }
    public bool Quotation { get; init; }
    public bool RequestedKg { get; init; }
    public bool ChipFirstPending { get; init; }
    public bool ChipToSample { get; init; }
    public bool HasTrial { get; init; }
    public bool HasSentKg { get; init; }
    public bool PendingTrial { get; init; }
    public bool Chip { get; init; }
    public bool Kg { get; init; }
    public bool Failed { get; init; }
    public bool Approved { get; init; }
    public bool UnknownSent { get; init; }
    public string? FormulaStatus { get; init; }
    public bool FinalizedChip { get; init; }
    public bool AcceptedPrice { get; init; }
    public bool RequiresPriceAcceptance { get; init; } = true;
    public bool RequiresCustomerPriceTracking { get; init; }
    public bool ApprovedPrice { get; init; }
    public bool LabConfirmed { get; init; }
    public bool Quoted { get; init; }
    public bool PriceAdjustmentRequired { get; init; }
    public bool RevisedFormulaNeedsPrice { get; init; }
    public bool PriceDecisionRequired { get; init; }
    public bool PriceDecisionDeferred { get; init; }
    public bool PriceNextStepRequired { get; init; }
    public bool LabFinalizationRequested { get; init; }
    public bool ChipFinalizationRequested { get; init; }
    public bool ChipDecisionDeferred { get; init; }
    public bool CompletionDeferred { get; init; }
    public bool ChipDeliveryRequested { get; init; }
    public bool DeliveryRequested { get; init; }
    public bool HasUnlinkedDeliveryCandidate { get; init; }
}

internal sealed record SampleWorkDecision
{
    public string Stage { get; init; } = "";
    public string Code { get; init; } = "";
    public string Role { get; init; } = "";
    public SampleWorkDecision() { }
    public SampleWorkDecision(string stage, string code, string role) => (Stage, Code, Role) = (stage, code, role);
}

/// <summary>One expression for SQL queue membership and the returned work assignment. No viewer permissions here.</summary>
internal static class SampleWorkDecisionRules
{
    // Phân nhóm chờ giá không thu hồi quyền gửi trước khi cần.
    internal static readonly Expression<Func<SampleWorkFacts, string, bool>> WaitForPriceBeforeSample = (f, code) =>
        f.Open && !f.Legacy && !f.LabPaused && !f.InternalDirect && f.RequestedKg &&
        (f.RequiresPriceAcceptance || f.RequiresCustomerPriceTracking) && !f.AcceptedPrice && code == "SendSample";
    internal static readonly Func<SampleWorkFacts, string, bool> WaitsForPriceBeforeSample = WaitForPriceBeforeSample.Compile();
    internal static readonly Expression<Func<SampleWorkFacts, bool>> SalePricePending = f =>
        f.Open && !f.Legacy && !f.LabPaused && f.LabConfirmed && f.ApprovedPrice &&
        (f.FormulaStatus == "Approved" || f.FormulaStatus == "SampleSent") && !f.AcceptedPrice &&
        !f.PriceAdjustmentRequired && !f.RevisedFormulaNeedsPrice;
    internal static readonly Expression<Func<SampleWorkFacts, bool>> SaleFeedbackPending = f =>
        f.Open && !f.Legacy && !f.LabPaused && f.PendingTrial && (f.Chip || f.Kg);
    private static readonly Func<SampleWorkFacts, bool> PricePendingCompiled = SalePricePending.Compile();
    private static readonly Func<SampleWorkFacts, bool> FeedbackPendingCompiled = SaleFeedbackPending.Compile();
    internal static bool HasSalePriceWork(SampleWorkFacts facts) => PricePendingCompiled(facts);
    internal static bool HasSaleFeedbackWork(SampleWorkFacts facts) => FeedbackPendingCompiled(facts);
    internal static readonly Expression<Func<SampleWorkFacts, string>> Action = f =>
        !f.Open ? "" : f.LabPaused ? "ResumeAfterLabRejection" : f.InternalDirect ? "ApproveFormula" : f.Legacy ? (f.Sent ? (f.FormulaStatus == "SampleSent" ? "CompleteRequest" : "ReviewSample") : f.FormulaStatus == "Approved" ? "SendSample" : "ApproveFormula") :
        f.ChipToSample ? (f.FormulaStatus == "Approved" || f.FormulaStatus == "SampleSent" ? "SendSample" : "ApproveFormula") :
        f.DeliveryRequested && !f.PendingTrial ? ((f.HasUnlinkedDeliveryCandidate || !f.HasSentKg && (f.FormulaStatus == "Approved" || f.FormulaStatus == "Rework" || f.FormulaStatus == "SampleSent" && f.Chip && f.Approved) || f.HasSentKg && (f.FormulaStatus == "SampleSent" || f.FormulaStatus == "Completed")) ? "SendSample" : "ApproveFormula") :
        f.ChipFirstPending ? (f.HasSentKg ? "ReviewSample" : f.PendingTrial ? (f.Chip ? "FollowUpCustomer" : "ReviewSample") :
            f.Chip && f.Failed ? "ReworkFormula" :
            f.FormulaStatus == "Approved" || f.FormulaStatus == "Rework" ? "SendSample" : "ApproveFormula") :
        f.Kg && f.PendingTrial ? "FollowUpCustomer" :
        f.Kg && f.Approved ? (!f.RequiresPriceAcceptance || f.AcceptedPrice ? "CompleteRequest" : !f.ApprovedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" : "PriceFeedback") :
        f.Quotation && f.LabConfirmed && f.FormulaStatus == "Draft" && !f.HasTrial && !f.HasSentKg ? "ApproveFormula" :
        f.CompletionDeferred && !f.PendingTrial && !f.HasSentKg ? "TrackRequest" :
        f.RequestedKg && !f.HasSentKg && (!f.HasTrial || (f.Chip && f.Approved)) &&
            !f.LabFinalizationRequested && !f.PriceDecisionRequired && !f.RevisedFormulaNeedsPrice && !f.PriceAdjustmentRequired ?
            (f.FormulaStatus == "Approved" || f.FormulaStatus == "SampleSent" && f.Chip && f.Approved || (f.FormulaStatus == "Completed" && f.FinalizedChip) ? "SendSample" : "ApproveFormula") :
        f.PriceNextStepRequired && f.AcceptedPrice && !f.PendingTrial && !f.HasSentKg && f.FormulaStatus != "Completed" ? "ChooseAfterPrice" :
        f.LabFinalizationRequested && f.AcceptedPrice && !f.PendingTrial && !f.HasSentKg ?
            (f.FormulaStatus == "Completed" ? "CompleteRequest" : f.FormulaStatus == "Approved" ? "FinalizeQuotation" : "ApproveFormula") :
        f.PriceDecisionRequired && !f.PendingTrial && !f.HasSentKg && (f.Quotation || f.RequestedKg) ? (f.PriceDecisionDeferred ? "TrackRejectedPrice" : "ChooseRejectedPriceAction") :
        f.RevisedFormulaNeedsPrice && !f.PendingTrial && !f.HasSentKg && (f.Quotation || f.RequestedKg) ? "ApproveStandardPrice" :
        f.PriceAdjustmentRequired && !f.PendingTrial && !f.HasSentKg && (f.Quotation || f.RequestedKg) ? "AdjustFormulaPrice" :
        f.Quotation && !f.RequestedKg ? (!f.LabConfirmed ? (f.FormulaStatus != null && f.FormulaStatus != "Draft" && f.FormulaStatus != "Rework" && f.FormulaStatus != "Approved" ? "ReviewSample" : "ApproveFormula") :
            !f.ApprovedPrice && !f.AcceptedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" :
            f.AcceptedPrice ? (f.FormulaStatus == "Completed" ? "CompleteRequest" : f.FormulaStatus == "Approved" ? "FinalizeQuotation" : "ReviewSample") : "PriceFeedback") :
        f.HasSentKg && !f.Kg ? "ReviewSample" :
        !f.HasTrial && f.Sent ? "ReviewSample" : f.PendingTrial ? (f.Chip || f.Kg ? "FollowUpCustomer" : "ReviewSample") :
        f.Kg ? (f.Failed ? (f.FormulaStatus == "Approved" ? "SendSample" : "ReworkFormula") : "") : f.UnknownSent ? "ReviewSample" :
        f.Chip && f.Approved && !f.RequestedKg && f.ChipDeliveryRequested ?
            (f.FormulaStatus == "Approved" || f.FormulaStatus == "Rework" || f.FormulaStatus == "SampleSent" ? "SendSample" : "ApproveFormula") :
        f.Chip && f.Approved && !f.RequestedKg && f.RequiresPriceAcceptance && !f.AcceptedPrice ?
            (!f.ApprovedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" : "PriceFeedback") :
        f.Chip && f.Approved && !f.RequestedKg ? (f.ChipDecisionDeferred ? "TrackApprovedChip" :
            f.FinalizedChip ? "CompleteRequest" : f.ChipFinalizationRequested ? "FinalizeChip" : "ChooseAfterChip") :
        f.RequestedKg ? (f.FormulaStatus == "Completed" && !f.FinalizedChip ? "ReviewSample" :
            f.FormulaStatus != "Approved" && f.FormulaStatus != "Completed" ? "ApproveFormula" :
            !f.AcceptedPrice ? (!f.ApprovedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" : "PriceFeedback") : "SendSample") :
        f.Chip && f.Approved ? "CompleteRequest" : f.Chip && f.Failed ? "ReworkFormula" :
        f.FormulaStatus == "Approved" || f.FormulaStatus == "Rework" ? "SendSample" : "ApproveFormula";

    private static readonly Expression<Func<SampleWorkFacts, string, string>> Stage = (f, code) =>
        code == "" ? "Closed" : code == "ResumeAfterLabRejection" ? "LabRejected" : f.InternalDirect ? "InternalFormulaCompletion" : code == "ReviewSample" ? "NeedsReview" : code == "CreateNewRequest" ? "NewRequestRequired" :
        f.Legacy ? (f.Sent ? "LegacySent" : "LegacyPreparation") :
        f.ChipFirstPending ? (code == "FollowUpCustomer" ? "AwaitingChipFeedback" : code == "ReworkFormula" ? "ReworkChip" : "PrepareChip") :
        code == "ChooseAfterPrice" ? "AwaitingSaleAfterPrice" :
        code == "TrackRequest" ? "RequestFollowUp" :
        code == "ChooseAfterChip" ? "AwaitingSaleAfterChip" : code == "TrackApprovedChip" ? "ChipFollowUp" :
        code == "AdjustFormulaPrice" ? "PriceRework" :
        code == "ChooseRejectedPriceAction" ? "AwaitingPriceDecision" : code == "TrackRejectedPrice" ? "PriceFollowUp" :
        code == "ApproveStandardPrice" ? "AwaitingStandardPrice" : code == "PriceFeedback" ? "AwaitingPriceFeedback" :
        code == "SendQuotation" ? (f.Quotation && !f.RequestedKg ? "QuotationPreparation" : "AwaitingPriceFeedback") :
        code == "FollowUpCustomer" ? (f.Chip ? "AwaitingChipFeedback" : "AwaitingSampleFeedback") :
        code == "FinalizeQuotation" ? "FinalizeQuotationFormula" : code == "FinalizeChip" ? "FinalizeChipFormula" : code == "CompleteRequest" ?
            (f.Quotation && !f.RequestedKg ? "QuotationAccepted" : "AwaitingSaleDecision") :
        code == "ReworkFormula" && f.Kg ? "ReworkSample" :
        f.Quotation && !f.RequestedKg ? "QuotationPreparation" : f.RequestedKg ?
            (code == "SendSample" ? "ReadyToSendSample" : "PrepareSample") : f.Failed ? "ReworkChip" : "PrepareChip";

    private static readonly Expression<Func<string, string>> Role = code =>
        code == "AdjustFormulaPrice" || code == "ApproveFormula" || code == "SendSample" || code == "ReworkFormula" || code == "FinalizeChip" || code == "FinalizeQuotation" ? "Lab" :
        code == "ApproveStandardPrice" ? "President" : code == "ReviewSample" ? "LabAdmin" : code == "" ? "" : "Sale";

    internal static readonly Expression<Func<SampleWorkFacts, SampleWorkDecision>> Expression = BuildExpression();
    private static readonly Func<SampleWorkFacts, SampleWorkDecision> ResolveCompiled = Expression.Compile();
    internal static SampleWorkDecision Resolve(SampleWorkFacts facts) => ResolveCompiled(facts);

    private static Expression<Func<SampleWorkFacts, SampleWorkDecision>> BuildExpression()
    {
        var facts = Action.Parameters[0];
        var code = Action.Body;
        var stage = Replace(Stage.Body, Stage.Parameters[0], facts);
        stage = Replace(stage, Stage.Parameters[1], code);
        var role = Replace(Role.Body, Role.Parameters[0], code);
        return System.Linq.Expressions.Expression.Lambda<Func<SampleWorkFacts, SampleWorkDecision>>(
            System.Linq.Expressions.Expression.MemberInit(System.Linq.Expressions.Expression.New(typeof(SampleWorkDecision)),
                System.Linq.Expressions.Expression.Bind(typeof(SampleWorkDecision).GetProperty(nameof(SampleWorkDecision.Stage))!, stage),
                System.Linq.Expressions.Expression.Bind(typeof(SampleWorkDecision).GetProperty(nameof(SampleWorkDecision.Code))!, code),
                System.Linq.Expressions.Expression.Bind(typeof(SampleWorkDecision).GetProperty(nameof(SampleWorkDecision.Role))!, role)), facts);
    }

    internal static Expression Replace(Expression body, ParameterExpression parameter, Expression value)
        => new ParameterReplacement(parameter, value).Visit(body)!;

    private sealed class ParameterReplacement(ParameterExpression parameter, Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? value : base.VisitParameter(node);
    }
}
