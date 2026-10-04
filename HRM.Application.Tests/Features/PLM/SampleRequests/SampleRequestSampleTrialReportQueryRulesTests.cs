using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSampleTrialReportQueryRulesTests
{
    [Fact]
    public void CompletedSamples_IncludesApprovedTrialWithoutFinishedDate()
    {
        var row = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.Approved,
            customerReplyDate: new DateTime(2026, 9, 3),
            finishedDate: null);

        var result = ApplyReportType([row], SampleTrialReportType.CompletedSamples);

        Assert.Same(row, Assert.Single(result));
    }

    [Fact]
    public void CompletedSamples_ExcludesNonApprovedLatestTrial()
    {
        var row = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.WaitingCustomerFeedback,
            customerReplyDate: null,
            finishedDate: new DateTime(2026, 9, 2));

        var result = ApplyReportType([row], SampleTrialReportType.CompletedSamples);

        Assert.Empty(result);
    }

    [Fact]
    public void All_DoesNotExcludeRecordsByWorkflowStatus()
    {
        var rows = new[]
        {
            CreateRow(SampleRequestStatus.New, SampleTrialStatus.Draft),
            CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.SampleSent),
            CreateRow(SampleRequestStatus.Completed, SampleTrialStatus.Approved),
            CreateRow(SampleRequestStatus.Cancelled, SampleTrialStatus.Cancelled)
        };

        var result = ApplyReportType(rows, SampleTrialReportType.All);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void DailyWorkToday_IncludesOnlyWorkDueOnSelectedDay()
    {
        var dueToday = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        dueToday.SampleRequest.ExpectedDeliveryDate = new DateTime(2026, 9, 23);
        var overdue = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        overdue.SampleRequest.ExpectedDeliveryDate = new DateTime(2023, 10, 14);
        overdue.SampleRequest.IsDelayed = true;
        var closed = CreateRow(SampleRequestStatus.Completed, SampleTrialStatus.Approved);
        closed.SampleRequest.ExpectedDeliveryDate = new DateTime(2026, 9, 23);
        var request = new GetSampleRequestSampleTrialsQuery
        {
            DailyWorkDate = new DateOnly(2026, 9, 23)
        };

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { dueToday, overdue, closed }.AsQueryable(), request).ToList();

        Assert.Same(dueToday, Assert.Single(result));
    }

    [Fact]
    public void DailyWorkAll_IncludesAllVisibleWorkflowStatuses()
    {
        var rows = new[]
        {
            CreateRow(SampleRequestStatus.New, SampleTrialStatus.Draft),
            CreateRow(SampleRequestStatus.Completed, SampleTrialStatus.Approved),
            CreateRow(SampleRequestStatus.Cancelled, SampleTrialStatus.Cancelled)
        };

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            rows.AsQueryable(), new GetSampleRequestSampleTrialsQuery
            {
                DailyWorkDate = new DateOnly(2026, 9, 23),
                DailyWorkView = SampleRequestDailyView.All
            }).ToList();

        Assert.Equal(rows, result);
    }

    [Fact]
    public void DailyWork_DueQuotationStopsAfterActualQuoteDate()
    {
        var due = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        due.SampleRequest.InfoType = "Báo giá";
        due.SampleRequest.ExpectedPriceQuoteDate = new DateTime(2026, 9, 23);
        var quoted = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        quoted.SampleRequest.InfoType = "Báo giá";
        quoted.SampleRequest.ExpectedPriceQuoteDate = new DateTime(2026, 9, 23);
        quoted.SampleRequest.RealPriceQuoteDate = new DateTime(2026, 9, 22);

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { due, quoted }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })
            .ToList();

        Assert.Same(due, Assert.Single(result));
    }

    [Fact]
    public void DailyWorkToday_IncludesFailedTrialWithFeedbackOnSelectedDay()
    {
        var failedToday = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Failed,
            customerReplyDate: new DateTime(2026, 9, 23, 14, 0, 0));
        var failedEarlier = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Failed,
            customerReplyDate: new DateTime(2026, 9, 22, 14, 0, 0));

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { failedToday, failedEarlier }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })
            .ToList();

        Assert.Same(failedToday, Assert.Single(result));
    }

    [Fact]
    public void DailyWork_SortsNewestSampleRequestFirst()
    {
        var older = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft,
            sampleRequestCreatedDate: new DateTime(2026, 9, 1));
        var newer = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft,
            sampleRequestCreatedDate: new DateTime(2026, 9, 20));

        var result = SampleRequestSampleTrialReportQueryRules.ApplySorting(
            new[] { older, newer }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })
            .ToList();

        Assert.Equal(new[] { newer, older }, result);
    }

    [Fact]
    public void CompletedSamples_DateRangeUsesCustomerReplyDate()
    {
        var inRange = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.Approved,
            sampleRequestCreatedDate: new DateTime(2026, 1, 10),
            customerReplyDate: new DateTime(2026, 6, 20));
        var outOfRange = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.Approved,
            sampleRequestCreatedDate: new DateTime(2026, 6, 10),
            customerReplyDate: new DateTime(2026, 7, 1));
        var request = new GetSampleRequestSampleTrialsQuery
        {
            ReportType = SampleTrialReportType.CompletedSamples,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30)
        };

        var result = SampleRequestSampleTrialReportQueryRules
            .ApplyDateRange(new[] { inRange, outOfRange }.AsQueryable(), request)
            .ToList();

        Assert.Same(inRange, Assert.Single(result));
    }

    [Fact]
    public void CompletedSamples_SortsByCustomerReplyDateDescending()
    {
        var olderApproval = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.Approved,
            sampleRequestCreatedDate: new DateTime(2026, 8, 20),
            customerReplyDate: new DateTime(2026, 9, 1));
        var newerApproval = CreateRow(
            SampleRequestStatus.Completed,
            SampleTrialStatus.Approved,
            sampleRequestCreatedDate: new DateTime(2026, 8, 1),
            customerReplyDate: new DateTime(2026, 9, 3));
        var request = new GetSampleRequestSampleTrialsQuery
        {
            ReportType = SampleTrialReportType.CompletedSamples
        };

        var result = SampleRequestSampleTrialReportQueryRules
            .ApplySorting(new[] { olderApproval, newerApproval }.AsQueryable(), request)
            .ToList();

        Assert.Equal(new[] { newerApproval, olderApproval }, result);
    }

    [Fact]
    public void WaitingCustomerFeedback_DateRangeUsesRequestReceivedDate()
    {
        var inRange = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            sampleRequestCreatedDate: new DateTime(2026, 6, 1),
            requestReceivedDate: new DateTime(2026, 5, 31),
            customerReplyStatus: "WAITING");
        var outOfRange = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            sampleRequestCreatedDate: new DateTime(2026, 5, 1),
            requestReceivedDate: new DateTime(2026, 6, 1),
            customerReplyStatus: "WAITING");
        var request = new GetSampleRequestSampleTrialsQuery
        {
            ReportType = SampleTrialReportType.WaitingCustomerFeedback,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 5, 31)
        };

        var result = SampleRequestSampleTrialReportQueryRules
            .ApplyDateRange(new[] { inRange, outOfRange }.AsQueryable(), request)
            .ToList();

        Assert.Same(inRange, Assert.Single(result));
    }

    [Fact]
    public void WaitingCustomerFeedback_IncludesUnreceivedSentSamplesBeforeCustomerFeedbackQueue()
    {
        var awaitingSaleReceipt = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.SampleSent,
            sentDate: new DateTime(2026, 9, 1));
        var awaitingCustomerFeedback = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            requestReceivedDate: new DateTime(2026, 9, 3),
            customerReplyStatus: "WAITING");
        var receivedButStillSent = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.SampleSent,
            sentDate: new DateTime(2026, 9, 4),
            requestReceivedDate: new DateTime(2026, 9, 4));

        var filtered = ApplyReportType(
            [awaitingCustomerFeedback, receivedButStillSent, awaitingSaleReceipt],
            SampleTrialReportType.WaitingCustomerFeedback);
        var sorted = SampleRequestSampleTrialReportQueryRules
            .ApplySorting(
                filtered.AsQueryable(),
                new GetSampleRequestSampleTrialsQuery
                {
                    ReportType = SampleTrialReportType.WaitingCustomerFeedback
                })
            .ToList();

        Assert.Equal([awaitingSaleReceipt, awaitingCustomerFeedback], sorted);
    }

    private static List<SampleRequestSampleTrialReportRow> ApplyReportType(
        IEnumerable<SampleRequestSampleTrialReportRow> rows,
        SampleTrialReportType reportType)
        => SampleRequestSampleTrialReportQueryRules
            .ApplyReportType(
                rows.AsQueryable(),
                new GetSampleRequestSampleTrialsQuery { ReportType = reportType })
            .ToList();

    private static SampleRequestSampleTrialReportRow CreateRow(
        SampleRequestStatus sampleRequestStatus,
        SampleTrialStatus trialStatus,
        DateTime? sampleRequestCreatedDate = null,
        DateTime? customerReplyDate = null,
        DateTime? requestReceivedDate = null,
        DateTime? sentDate = null,
        DateTime? finishedDate = null,
        string? customerReplyStatus = null)
        => new()
        {
            SampleRequest = new SampleRequest
            {
                Status = sampleRequestStatus.ToString(),
                CreatedDate = sampleRequestCreatedDate ?? new DateTime(2026, 1, 1),
                IsActive = true
            },
            Trial = new SampleRequestSampleTrial
            {
                Status = trialStatus,
                CustomerReplyDate = customerReplyDate,
                CustomerReplyStatus = customerReplyStatus,
                RequestReceivedDate = requestReceivedDate,
                SentDate = sentDate,
                FinishedDate = finishedDate,
                CreatedDate = sampleRequestCreatedDate ?? new DateTime(2026, 1, 1),
                IsActive = true
            },
            TrialCount = 1
        };
}
