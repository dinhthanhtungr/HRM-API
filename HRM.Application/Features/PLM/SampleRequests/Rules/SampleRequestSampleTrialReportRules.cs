namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestSampleTrialReportRules
{
    public static SampleRequestCreatedRange ResolveCreatedRange(
        DateOnly? createdToDate,
        bool includePreviousUnfinished)
    {
        if (!createdToDate.HasValue)
        {
            return new SampleRequestCreatedRange(null, null);
        }

        var fromInclusive = includePreviousUnfinished
            ? (DateTime?)null
            : new DateTime(createdToDate.Value.Year, createdToDate.Value.Month, 1);

        return new SampleRequestCreatedRange(
            fromInclusive,
            ResolveCreatedToExclusive(createdToDate));
    }

    public static DateTime? ResolveCreatedToExclusive(DateOnly? createdToDate)
    {
        if (!createdToDate.HasValue)
        {
            return null;
        }

        if (createdToDate.Value == DateOnly.MaxValue)
        {
            return DateTime.MaxValue;
        }

        return createdToDate.Value
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue);
    }

    public static int? CalculateTurnaroundDays(DateTime? receivedDate, DateTime? finishedDate)
    {
        if (!receivedDate.HasValue || !finishedDate.HasValue)
        {
            return null;
        }

        return Math.Max(0, (finishedDate.Value.Date - receivedDate.Value.Date).Days);
    }
}

internal sealed record SampleRequestCreatedRange(
    DateTime? FromInclusive,
    DateTime? ToExclusive);
