using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal static class SampleRequestSampleTrialReportQueryRules
{
    // The persisted action payload always includes formulaId, including JSON null for ProductOnly.
    internal static IQueryable<InternalMessage> FormulaPriceQuoteRequests(IQueryable<InternalMessage> messages)
        => messages.Where(message => !message.IsDeleted && message.Conversation.IsActive &&
            message.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
            message.PayloadJson != null &&
            EF.Functions.JsonContains(message.PayloadJson,
                "{\"contentType\":\"SampleRequestPriceQuoteRequested\",\"priceQuoteRequest\":{}}") &&
            !EF.Functions.JsonContains(message.PayloadJson,
                "{\"priceQuoteRequest\":{\"formulaId\":null}}"));

    public static IQueryable<SampleRequestSampleTrialReportRow> ApplyReportType(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        if (request.DailyWorkDate.HasValue)
        {
            if (request.DailyWorkView == SampleRequestDailyView.All)
                return query;
            if (request.DailyWorkView == SampleRequestDailyView.Tracking)
                return query.Where(x => x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled");
            if (request.DailyWorkView is SampleRequestDailyView.SalePending or SampleRequestDailyView.SaleToday)
                return query.Where(x => x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled" &&
                    ((x.SampleRequest.Status == "WaitingCustomerPriceConfirmation" && x.LabFormulaConfirmedAt.HasValue) ||
                     (x.Trial != null && (x.Trial.Status == SampleTrialStatus.SampleSent ||
                      x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback || x.Trial.Status == SampleTrialStatus.PriceQuote)) ||
                     (x.LabFormulaConfirmedAt.HasValue && !x.SampleRequest.RealPriceQuoteDate.HasValue &&
                      ((x.SampleRequest.InfoType ?? "").ToLower() == "quotation" ||
                       (x.SampleRequest.InfoType ?? "").ToLower() == "báo giá"))));

            if (request.DailyWorkView == SampleRequestDailyView.Delay)
                return query.Where(x => x.SampleRequest.Status != "Completed" &&
                    x.SampleRequest.Status != "Cancelled" && x.SampleRequest.IsDelayed == true);

            // Delay thủ công có thể đưa hồ sơ Lab đã xong trở lại danh sách cần theo dõi.
            return query.Where(x =>
                x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled" &&
                (x.SampleRequest.IsDelayed == true ||
                 (
                 (x.Trial == null || (x.Trial.Status != SampleTrialStatus.Approved &&
                                      x.Trial.Status != SampleTrialStatus.Cancelled)) &&
                 (((x.SampleRequest.InfoType ?? string.Empty).Trim().ToLower() == "quotation" ||
                   (x.SampleRequest.InfoType ?? string.Empty).Trim().ToLower() == "báo giá")
                     ? !x.LabFormulaConfirmedAt.HasValue ||
                       (x.Trial != null && x.Trial.Status == SampleTrialStatus.Failed &&
                        x.LabFormulaConfirmedAt <= (x.Trial.CustomerReplyDate ?? x.Trial.UpdatedDate ?? x.Trial.CreatedDate)) ||
                       (x.Trial != null && x.Trial.Status == SampleTrialStatus.Draft &&
                        x.LabFormulaConfirmedAt < x.Trial.CreatedDate)
                     : (x.Trial != null && (x.Trial.Status == SampleTrialStatus.Failed || x.Trial.Status == SampleTrialStatus.Draft)) ||
                       x.SampleRequest.Status != "SampleSent" &&
                       (x.Trial == null || (x.Trial.Status != SampleTrialStatus.SampleSent &&
                                           x.Trial.Status != SampleTrialStatus.WaitingCustomerFeedback))))));
        }

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
                (x.Trial.Status == SampleTrialStatus.SampleSent ||
                 x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                 x.Trial.Status == SampleTrialStatus.PriceQuote)),
            _ => query
        };
    }

    public static IQueryable<SampleRequestSampleTrialReportRow> ApplyDateRange(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        if (request.DailyWorkDate.HasValue)
        {
            return query;
        }

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
                    x.Trial.SentDate >= fromInclusive.Value);
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(x =>
                    x.Trial != null &&
                    x.Trial.SentDate < toExclusive.Value);
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
        if (request.DailyWorkDate.HasValue)
        {
            return query
                .OrderByDescending(x => x.SampleRequest.CreatedDate)
                .ThenBy(x => x.SampleRequest.ExternalId);
        }

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
                x.Trial != null ? x.Trial.SentDate ?? x.Trial.CreatedDate : x.SampleRequest.CreatedDate),
            _ => query.OrderByDescending(x => x.SampleRequest.CreatedDate)
        };
    }
}
