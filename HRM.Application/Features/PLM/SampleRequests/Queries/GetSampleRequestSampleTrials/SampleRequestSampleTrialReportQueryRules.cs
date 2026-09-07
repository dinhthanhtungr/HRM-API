using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal static class SampleRequestSampleTrialReportQueryRules
{
    public static IQueryable<SampleRequestSampleTrialReportRow> ApplyReportType(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        if (request.IncludeTrialHistory)
        {
            return query;
        }

        return request.ReportType switch
        {
            SampleTrialReportType.CompletedSamples => query.Where(x =>
                x.Trial != null &&
                x.Trial.Status == SampleTrialStatus.Approved &&
                x.SampleRequest.Status == SampleRequestStatus.Completed.ToString()),
            SampleTrialReportType.WaitingCustomerFeedback => query.Where(x =>
                x.Trial != null &&
                ((x.Trial.Status == SampleTrialStatus.SampleSent &&
                  !x.Trial.RequestReceivedDate.HasValue) ||
                 (x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback &&
                  x.Trial.RequestReceivedDate.HasValue &&
                  (x.Trial.CustomerReplyStatus == null ||
                   x.Trial.CustomerReplyStatus == string.Empty ||
                   x.Trial.CustomerReplyStatus == "WAITING")))),
            _ => query
        };
    }

    public static IQueryable<SampleRequestSampleTrialReportRow> ApplyDateRange(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        if (!request.FromDate.HasValue && !request.ToDate.HasValue)
        {
            return query;
        }

        var fromInclusive = request.FromDate?.Date;
        var toExclusive = request.ToDate?.Date.AddDays(1);

        if (request.IncludeTrialHistory)
        {
            if (fromInclusive.HasValue)
            {
                query = query.Where(x =>
                    (x.Trial != null
                        ? x.Trial.FinishedDate ?? x.Trial.SentDate ?? x.Trial.RequestReceivedDate ?? x.Trial.CreatedDate
                        : x.SampleRequest.CreatedDate) >= fromInclusive.Value);
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(x =>
                    (x.Trial != null
                        ? x.Trial.FinishedDate ?? x.Trial.SentDate ?? x.Trial.RequestReceivedDate ?? x.Trial.CreatedDate
                        : x.SampleRequest.CreatedDate) < toExclusive.Value);
            }

            return query;
        }

        if (request.ReportType == SampleTrialReportType.CompletedSamples)
        {
            if (fromInclusive.HasValue)
            {
                query = query.Where(x =>
                    x.Trial != null && x.Trial.CustomerReplyDate >= fromInclusive.Value);
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(x =>
                    x.Trial != null && x.Trial.CustomerReplyDate < toExclusive.Value);
            }

            return query;
        }

        if (request.ReportType == SampleTrialReportType.WaitingCustomerFeedback)
        {
            if (fromInclusive.HasValue)
            {
                query = query.Where(x =>
                    x.Trial != null &&
                    (x.Trial.Status == SampleTrialStatus.SampleSent
                        ? x.Trial.SentDate >= fromInclusive.Value
                        : x.Trial.RequestReceivedDate >= fromInclusive.Value));
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(x =>
                    x.Trial != null &&
                    (x.Trial.Status == SampleTrialStatus.SampleSent
                        ? x.Trial.SentDate < toExclusive.Value
                        : x.Trial.RequestReceivedDate < toExclusive.Value));
            }

            return query;
        }

        if (fromInclusive.HasValue)
        {
            query = query.Where(x => x.SampleRequest.CreatedDate >= fromInclusive.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(x => x.SampleRequest.CreatedDate < toExclusive.Value);
        }

        return query;
    }

    public static IQueryable<SampleRequestSampleTrialReportRow> ApplySorting(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        if (request.IncludeTrialHistory)
        {
            return query.OrderByDescending(x => x.Trial != null ? x.Trial.TrialNo : (int?)null);
        }

        return request.ReportType switch
        {
            SampleTrialReportType.CompletedSamples => query.OrderByDescending(x =>
                x.Trial != null
                    ? x.Trial.CustomerReplyDate ?? x.Trial.UpdatedDate ?? x.Trial.CreatedDate
                    : x.SampleRequest.CreatedDate),
            SampleTrialReportType.WaitingCustomerFeedback => query.OrderByDescending(x =>
                x.Trial != null && x.Trial.Status == SampleTrialStatus.SampleSent)
                .ThenByDescending(x =>
                x.Trial != null
                    ? x.Trial.Status == SampleTrialStatus.SampleSent
                        ? x.Trial.SentDate ?? x.Trial.UpdatedDate ?? x.Trial.CreatedDate
                        : x.Trial.RequestReceivedDate ?? x.Trial.UpdatedDate ?? x.Trial.CreatedDate
                    : x.SampleRequest.CreatedDate),
            _ => query.OrderByDescending(x => x.SampleRequest.CreatedDate)
        };
    }
}
