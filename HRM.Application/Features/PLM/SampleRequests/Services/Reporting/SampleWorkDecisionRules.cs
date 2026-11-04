using System.Linq.Expressions;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal sealed record SampleWorkFacts
{
    public bool Open { get; init; }
    public bool Legacy { get; init; }
    public bool Sent { get; init; }
    public bool Quotation { get; init; }
    public bool RequestedKg { get; init; }
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
    public bool ApprovedPrice { get; init; }
    public bool LabConfirmed { get; init; }
    public bool Quoted { get; init; }
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
    internal static readonly Expression<Func<SampleWorkFacts, string>> Action = f =>
        !f.Open ? "" : f.Legacy ? (f.Sent ? (f.FormulaStatus == "SampleSent" ? "CompleteRequest" : "ReviewSample") : f.FormulaStatus == "Approved" ? "SendSample" : "ApproveFormula") :
        f.Quotation && !f.RequestedKg ? (!f.LabConfirmed ? (f.FormulaStatus != null && f.FormulaStatus != "Draft" && f.FormulaStatus != "Rework" && f.FormulaStatus != "Approved" ? "ReviewSample" : "ApproveFormula") :
            !f.ApprovedPrice && !f.AcceptedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" :
            f.AcceptedPrice ? (f.FormulaStatus == "Completed" ? "CompleteRequest" : f.FormulaStatus == "Approved" ? "FinalizeQuotation" : "ReviewSample") : "PriceFeedback") :
        f.HasSentKg && !f.Kg ? "ReviewSample" :
        !f.HasTrial && f.Sent ? "ReviewSample" : f.PendingTrial ? (f.Chip || f.Kg ? "FollowUpCustomer" : "ReviewSample") :
        f.Kg ? (f.Failed ? "CreateNewRequest" : "") : f.UnknownSent ? "ReviewSample" :
        f.Chip && f.Approved && !f.FinalizedChip ? "FinalizeChip" :
        f.RequestedKg ? (f.FormulaStatus == "Completed" && !f.FinalizedChip ? "ReviewSample" :
            f.FormulaStatus != "Approved" && f.FormulaStatus != "Completed" ? "ApproveFormula" :
            !f.AcceptedPrice ? (!f.ApprovedPrice ? "ApproveStandardPrice" : !f.Quoted ? "SendQuotation" : "PriceFeedback") : "SendSample") :
        f.Chip && f.Approved ? "CompleteRequest" : f.Chip && f.Failed ? "ReworkFormula" :
        f.FormulaStatus == "Approved" || f.FormulaStatus == "Rework" ? "SendSample" : "ApproveFormula";

    private static readonly Expression<Func<SampleWorkFacts, string, string>> Stage = (f, code) =>
        code == "" ? "Closed" : code == "ReviewSample" ? "NeedsReview" : code == "CreateNewRequest" ? "NewRequestRequired" :
        f.Legacy ? (f.Sent ? "LegacySent" : "LegacyPreparation") :
        code == "ApproveStandardPrice" ? "AwaitingStandardPrice" : code == "PriceFeedback" ? "AwaitingPriceFeedback" :
        code == "SendQuotation" ? (f.Quotation && !f.RequestedKg ? "QuotationPreparation" : "AwaitingPriceFeedback") :
        code == "FollowUpCustomer" ? (f.Chip ? "AwaitingChipFeedback" : "AwaitingSampleFeedback") :
        code == "FinalizeQuotation" ? "FinalizeQuotationFormula" : code == "FinalizeChip" ? "FinalizeChipFormula" : code == "CompleteRequest" ?
            (f.Quotation && !f.RequestedKg ? "QuotationAccepted" : "AwaitingSaleDecision") :
        f.Quotation && !f.RequestedKg ? "QuotationPreparation" : f.RequestedKg ?
            (code == "SendSample" ? "ReadyToSendSample" : "PrepareSample") : f.Failed ? "ReworkChip" : "PrepareChip";

    private static readonly Expression<Func<string, string>> Role = code =>
        code == "ApproveFormula" || code == "SendSample" || code == "ReworkFormula" || code == "FinalizeChip" || code == "FinalizeQuotation" ? "Lab" :
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
