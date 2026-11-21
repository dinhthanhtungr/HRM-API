using HRM.Application.Features.PLM.SampleRequests.Services;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

/// <summary>Presentation priority only: never changes queue membership or workflow permissions.</summary>
internal static class SampleWorkTaskPriority
{
    internal static readonly string[] Codes = ["NewRequest", "ApproveFormula", "SendChip", "SendSample", "ReworkFormula",
        "FinalizeFormula", "FinalizeQuotation", "FinalizeChip", "SendQuotation", "PriceFeedback", "SampleFeedback", "ChipFeedback",
        "ResumeAfterLabRejection", "ReviewSample", "Schedule"];

    internal static string[] Normalize(IEnumerable<string>? values)
        => Codes.Where(code => values?.Contains(code, StringComparer.Ordinal) == true).ToArray();

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
            else if (decision.Role == "Lab")
            {
                if (decision.Code is "FinalizeQuotation" or "FinalizeChip") tasks.Add("FinalizeFormula");
                tasks.Add(decision.Code == "SendSample" && !facts.Legacy && (!facts.RequestedKg || facts.ChipFirstPending)
                    ? "SendChip" : decision.Code);
            }
        }
        return Codes.Where(tasks.Contains).ToArray();
    }
}
