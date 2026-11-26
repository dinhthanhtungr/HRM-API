using System.Linq.Expressions;
using HRM.Application.Features.PLM.SampleRequests.Services;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

/// <summary>Work types for filters, priority and facets within the authorized queue.</summary>
internal static class SampleWorkTaskPriority
{
    internal static readonly string[] Codes = ["NewRequest", "ApproveFormula", "SendChip", "SendSample", "ReworkFormula",
        "FinalizeFormula", "FinalizeQuotation", "FinalizeChip", "SendQuotation", "PriceFeedback", "SampleFeedback", "ChipFeedback",
        "ResumeAfterLabRejection", "ReviewSample", "Schedule"];

    internal static string[] Normalize(IEnumerable<string>? values)
        => Codes.Where(code => values?.Contains(code, StringComparer.Ordinal) == true).ToArray();

    internal static bool IsLabFilter(string code)
        => code is "FinalizeFormula" or "ApproveFormula" or "SendSample" or "SendChip";

    // Four priority tiers ahead of the ordinary work ranks.
    internal static readonly Expression<Func<SampleWorkFacts, string, int>> LabRank = (facts, code) =>
        code == "FinalizeQuotation" || code == "FinalizeChip" || (facts.InternalDirect && code == "ApproveFormula") ? -4 :
        code == "SendSample" ? (!facts.Legacy && (!facts.RequestedKg || facts.ChipFirstPending) ? -2 : -3) :
        code == "ApproveFormula" && facts.FormulaStatus != null ? -1 : 2;

    internal static readonly Func<SampleWorkFacts, string, int> ResolveLabRank = LabRank.Compile();

    internal static bool IsSaleFilter(string code)
        => code is "SendQuotation" or "PriceFeedback" or "SampleFeedback" or "ChipFeedback" or
            "ResumeAfterLabRejection" or "ReviewSample";

    internal static string[] Tasks(SampleWorkFacts facts, SampleWorkDecision decision, SampleRequestWorkAudience? audience, bool needsSchedule, string requestStatus)
    {
        var tasks = new HashSet<string>(StringComparer.Ordinal);
        if (audience == SampleRequestWorkAudience.Sale)
        {
            if (SampleWorkDecisionRules.HasSalePriceWork(facts)) tasks.Add("PriceFeedback");
            if (SampleWorkDecisionRules.HasSaleFeedbackWork(facts)) tasks.Add(facts.Kg ? "SampleFeedback" : "ChipFeedback");
            if (decision.Role == "Sale" && Codes.Contains(decision.Code)) tasks.Add(decision.Code);
        }
        else
        {
            if (audience == SampleRequestWorkAudience.LabAdmin && needsSchedule) tasks.Add("Schedule");
            if (facts.Open && decision.Role == "Lab" && requestStatus == "New")
                tasks.Add("NewRequest");
            if (decision.Role == "Lab")
            {
                if (decision.Code is "FinalizeQuotation" or "FinalizeChip" || (facts.InternalDirect && decision.Code == "ApproveFormula"))
                    tasks.Add("FinalizeFormula");
                if (!facts.InternalDirect || decision.Code != "ApproveFormula")
                    tasks.Add(decision.Code == "SendSample" && !facts.Legacy && (!facts.RequestedKg || facts.ChipFirstPending)
                        ? "SendChip" : decision.Code);
            }
        }
        return Codes.Where(tasks.Contains).ToArray();
    }
}
