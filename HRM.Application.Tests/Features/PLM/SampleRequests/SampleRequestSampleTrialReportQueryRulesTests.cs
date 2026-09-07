using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
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
