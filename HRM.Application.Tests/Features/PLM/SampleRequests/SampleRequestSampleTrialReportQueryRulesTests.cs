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
    public void DailyWorkToday_IncludesUnfinishedWorkWithOrWithoutDelayRegardlessOfDueDate()
    {
        var dueToday = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        dueToday.SampleRequest.CreatedDate = new DateTime(2026, 9, 1);
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

        Assert.Equal(new[] { dueToday, overdue }, result);
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
    public void DailyWork_QuotationStopsAtFormulaPriceRequest_NotSaleQuoteDate()
    {
        var due = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        due.SampleRequest.InfoType = "Báo giá";
        due.SampleRequest.CreatedDate = new DateTime(2026, 9, 1);
        due.SampleRequest.ExpectedPriceQuoteDate = new DateTime(2026, 9, 23);
        due.SampleRequest.IsDelayed = false;
        due.SampleRequest.RealPriceQuoteDate = new DateTime(2026, 9, 22);
        var quoted = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        quoted.SampleRequest.InfoType = "Báo giá";
        quoted.SampleRequest.CreatedDate = new DateTime(2026, 9, 1);
        quoted.SampleRequest.ExpectedPriceQuoteDate = new DateTime(2026, 9, 23);
        quoted.SampleRequest.RealPriceQuoteDate = new DateTime(2026, 9, 22);
        quoted.SampleRequest.IsDelayed = false;
        quoted.LabFormulaConfirmedAt = new DateTime(2026, 9, 22);

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { due, quoted }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })
            .ToList();

        Assert.Same(due, Assert.Single(result));
    }

    [Fact]
    public void DailyWorkToday_IncludesFailedWorkRegardlessOfFeedbackDateOrDelay()
    {
        var failedToday = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Failed,
            customerReplyDate: new DateTime(2026, 9, 23, 14, 0, 0));
        failedToday.SampleRequest.CreatedDate = new DateTime(2026, 9, 1);
        var failedEarlier = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Failed,
            customerReplyDate: new DateTime(2026, 9, 22, 14, 0, 0));
        failedEarlier.SampleRequest.IsDelayed = true;

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { failedToday, failedEarlier }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })
            .ToList();

        Assert.Equal(new[] { failedToday, failedEarlier }, result);
    }

    [Theory]
    [InlineData(SampleRequestStatus.Completed, SampleTrialStatus.Approved)]
    [InlineData(SampleRequestStatus.Cancelled, SampleTrialStatus.Cancelled)]
    public void DailyWorkToday_ExcludesFinishedLabWorkEvenWithDelay(
        SampleRequestStatus requestStatus, SampleTrialStatus trialStatus)
    {
        var row = CreateRow(requestStatus, trialStatus);
        row.SampleRequest.IsDelayed = true;

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { row }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) });

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(SampleRequestStatus.InProgress, SampleTrialStatus.SampleSent)]
    [InlineData(SampleRequestStatus.InProgress, SampleTrialStatus.WaitingCustomerFeedback)]
    [InlineData(SampleRequestStatus.InProgress, SampleTrialStatus.Approved)]
    public void DailyWorkToday_FinishedLabWorkAppearsOnlyWhileDelayed(
        SampleRequestStatus requestStatus, SampleTrialStatus trialStatus)
    {
        var row = CreateRow(requestStatus, trialStatus);
        var request = new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) };
        row.SampleRequest.CreatedDate = new DateTime(2026, 9, 1);
        Assert.Empty(SampleRequestSampleTrialReportQueryRules.ApplyReportType(new[] { row }.AsQueryable(), request));

        row.SampleRequest.IsDelayed = true;
        Assert.Same(row, Assert.Single(SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { row }.AsQueryable(), request)));
    }

    [Fact]
    public void DailyWorkToday_RequestedQuotationStillAppearsWhileDelayed()
    {
        var row = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        row.SampleRequest.InfoType = "Quotation";
        row.LabFormulaConfirmedAt = new DateTime(2026, 9, 22);
        row.SampleRequest.IsDelayed = true;

        Assert.Same(row, Assert.Single(SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { row }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) })));
    }

    [Fact]
    public void DailyWorkToday_IncludesDelayedRequestWithoutTrialOrDueDate()
    {
        var row = CreateRow(SampleRequestStatus.New, SampleTrialStatus.Draft);
        row.SampleRequest.IsDelayed = true;
        row.Trial = null;

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { row }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 9, 23) });

        Assert.Same(row, Assert.Single(result));
    }

    [Fact]
    public void DelayView_OnlyIncludesManuallyMarkedOpenRequests_EvenAfterLabFinished()
    {
        var unfinished = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        var sent = CreateRow(SampleRequestStatus.SampleSent, SampleTrialStatus.SampleSent);
        sent.SampleRequest.IsDelayed = true;
        var closed = CreateRow(SampleRequestStatus.Completed, SampleTrialStatus.Approved);
        closed.SampleRequest.IsDelayed = true;
        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(
            new[] { unfinished, sent, closed }.AsQueryable(), new GetSampleRequestSampleTrialsQuery
            {
                DailyWorkDate = new DateOnly(2026, 10, 30), DailyWorkView = SampleRequestDailyView.Delay
            });
        Assert.Same(sent, Assert.Single(result));
    }

    [Fact]
    public void FailedQuotation_ReappearsUntilConfirmedAfterFeedback()
    {
        var feedback = new DateTime(2026, 10, 30, 12, 0, 0);
        var row = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Failed, customerReplyDate: feedback);
        row.SampleRequest.CreatedDate = feedback.Date;
        row.SampleRequest.InfoType = "Quotation";
        row.LabFormulaConfirmedAt = feedback.AddDays(-1);
        var request = new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 30) };
        Assert.Same(row, Assert.Single(SampleRequestSampleTrialReportQueryRules.ApplyReportType(new[] { row }.AsQueryable(), request)));
        row.LabFormulaConfirmedAt = feedback.AddMinutes(1);
        Assert.Empty(SampleRequestSampleTrialReportQueryRules.ApplyReportType(new[] { row }.AsQueryable(), request));
    }

    [Fact]
    public void QuotationType_IsTrimmedAndCaseInsensitive()
    {
        var row = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft);
        row.SampleRequest.InfoType = "  qUoTaTiOn  ";
        row.SampleRequest.CreatedDate = new DateTime(2026, 10, 1);
        row.LabFormulaConfirmedAt = new DateTime(2026, 10, 30);
        Assert.Empty(SampleRequestSampleTrialReportQueryRules.ApplyReportType(new[] { row }.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 30) }));
    }

    [Theory]
    [InlineData(2026, 10)]
    [InlineData(2026, 12)]
    [InlineData(2024, 2)]
    [InlineData(9999, 12)]
    public void Today_DoesNotReapplyCreationMonthAfterOperationalScope(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);
        var firstRow = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft, start);
        var lastRow = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft, last);
        var old = CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft, start.AddDays(-1));
        old.Trial!.CreatedDate = start; // Trial mới không được kéo hồ sơ cũ vào tháng hiện tại.
        var pinned = CreateRow(SampleRequestStatus.SampleSent, SampleTrialStatus.SampleSent, start.AddMonths(-1));
        pinned.SampleRequest.IsDelayed = true;
        var rows = new List<SampleRequestSampleTrialReportRow> { firstRow, lastRow, old, pinned };
        if (year != 9999 || month != 12)
            rows.Add(CreateRow(SampleRequestStatus.InProgress, SampleTrialStatus.Draft, start.AddMonths(1)));

        var result = SampleRequestSampleTrialReportQueryRules.ApplyReportType(rows.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(year, month, 15) });

        Assert.Equal(rows, result);
        pinned.SampleRequest.IsDelayed = false;
        Assert.DoesNotContain(pinned, SampleRequestSampleTrialReportQueryRules.ApplyReportType(rows.AsQueryable(),
            new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(year, month, 15) }));
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
    public void WaitingCustomerFeedback_DateRangeUsesSentDateNotLegacySaleReceipt()
    {
        var inRange = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            sampleRequestCreatedDate: new DateTime(2026, 6, 1),
            requestReceivedDate: new DateTime(2026, 5, 31),
            sentDate: new DateTime(2026, 5, 30),
            customerReplyStatus: "WAITING");
        var outOfRange = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            sampleRequestCreatedDate: new DateTime(2026, 5, 1),
            requestReceivedDate: new DateTime(2026, 6, 1),
            sentDate: new DateTime(2026, 6, 1),
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
    public void WaitingCustomerFeedback_IncludesSentTrialsWithoutReceiptAndSortsByDelivery()
    {
        var awaitingSaleReceipt = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.SampleSent,
            sentDate: new DateTime(2026, 9, 1));
        var awaitingCustomerFeedback = CreateRow(
            SampleRequestStatus.SampleSent,
            SampleTrialStatus.WaitingCustomerFeedback,
            requestReceivedDate: new DateTime(2026, 9, 3),
            sentDate: new DateTime(2026, 9, 3),
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

        Assert.Equal([receivedButStillSent, awaitingCustomerFeedback, awaitingSaleReceipt], sorted);
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
