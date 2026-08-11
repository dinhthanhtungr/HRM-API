namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestSampleTrialReportRules
{
    public static int? CalculateTurnaroundDays(DateTime? receivedDate, DateTime? finishedDate)
    {
        if (!receivedDate.HasValue || !finishedDate.HasValue)
        {
            return null;
        }

        return Math.Max(0, (finishedDate.Value.Date - receivedDate.Value.Date).Days);
    }
}
