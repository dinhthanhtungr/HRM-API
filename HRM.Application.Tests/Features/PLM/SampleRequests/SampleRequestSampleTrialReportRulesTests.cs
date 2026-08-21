using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;
using System.Text.Json;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSampleTrialReportRulesTests
{
    [Fact]
    public void ResolveCreatedToExclusive_ReturnsStartOfNextDay()
    {
        var result = SampleRequestSampleTrialReportRules.ResolveCreatedToExclusive(
            new DateOnly(2026, 7, 31));

        Assert.Equal(new DateTime(2026, 8, 1), result);
    }

    [Fact]
    public void ResolveCreatedToExclusive_ReturnsNullWhenCutoffIsMissing()
    {
        Assert.Null(SampleRequestSampleTrialReportRules.ResolveCreatedToExclusive(null));
    }

    [Fact]
    public void ResolveCreatedToExclusive_DoesNotOverflowAtMaximumDate()
    {
        Assert.Equal(
            DateTime.MaxValue,
            SampleRequestSampleTrialReportRules.ResolveCreatedToExclusive(DateOnly.MaxValue));
    }

    [Fact]
    public void ResolveCreatedRange_IncludesPreviousMonthsWhenFlagIsTrue()
    {
        var result = SampleRequestSampleTrialReportRules.ResolveCreatedRange(
            new DateOnly(2026, 7, 31),
            includePreviousUnfinished: true);

        Assert.Null(result.FromInclusive);
        Assert.Equal(new DateTime(2026, 8, 1), result.ToExclusive);
    }

    [Fact]
    public void ResolveCreatedRange_UsesCutoffMonthWhenFlagIsFalse()
    {
        var result = SampleRequestSampleTrialReportRules.ResolveCreatedRange(
            new DateOnly(2026, 7, 31),
            includePreviousUnfinished: false);

        Assert.Equal(new DateTime(2026, 7, 1), result.FromInclusive);
        Assert.Equal(new DateTime(2026, 8, 1), result.ToExclusive);
    }

    [Fact]
    public void ResolveCreatedRange_DoesNotFilterWithoutCutoff()
    {
        var result = SampleRequestSampleTrialReportRules.ResolveCreatedRange(
            null,
            includePreviousUnfinished: false);

        Assert.Null(result.FromInclusive);
        Assert.Null(result.ToExclusive);
    }

    [Fact]
    public void CalculateTurnaroundDays_UsesCalendarDates()
    {
        var received = new DateTime(2026, 6, 1, 23, 30, 0);
        var finished = new DateTime(2026, 6, 11, 1, 0, 0);

        var result = SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(received, finished);

        Assert.Equal(10, result);
    }

    [Fact]
    public void CalculateTurnaroundDays_DoesNotReturnNegativeValue()
    {
        var result = SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(
            new DateTime(2026, 6, 11),
            new DateTime(2026, 6, 1));

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateTurnaroundDays_ReturnsNullWhenDateIsMissing()
    {
        Assert.Null(SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(null, DateTime.Now));
        Assert.Null(SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(DateTime.Now, null));
    }

    [Fact]
    public void Query_NormalizesPaginationAndKeyword()
    {
        var query = new GetSampleRequestSampleTrialsQuery
        {
            PageNumber = 0,
            PageSize = 500,
            Keyword = "  TP_29153  "
        };

        Assert.Equal(1, query.NormalizedPageNumber);
        Assert.Equal(100, query.NormalizedPageSize);
        Assert.Equal("TP_29153", query.NormalizedKeyword);
    }

    [Fact]
    public void Query_SupportsExplicitReportTypesWithoutChangingDefault()
    {
        Assert.Null(new GetSampleRequestSampleTrialsQuery().ReportType);
        Assert.Equal(
            SampleTrialReportType.CompletedSamples,
            new GetSampleRequestSampleTrialsQuery
            {
                ReportType = SampleTrialReportType.CompletedSamples
            }.ReportType);
        Assert.Equal(
            SampleTrialReportType.WaitingCustomerFeedback,
            new GetSampleRequestSampleTrialsQuery
            {
                ReportType = SampleTrialReportType.WaitingCustomerFeedback
            }.ReportType);
    }

    [Fact]
    public void Query_SupportsIndependentSampleRequestCreatedDateCutoff()
    {
        var cutoff = new DateOnly(2026, 7, 31);

        var query = new GetSampleRequestSampleTrialsQuery
        {
            ReportType = SampleTrialReportType.WaitingCustomerFeedback,
            SampleRequestCreatedToDate = cutoff
        };

        Assert.Equal(cutoff, query.SampleRequestCreatedToDate);
        Assert.True(query.IncludePreviousUnfinished);
        Assert.Null(query.FromDate);
        Assert.Null(query.ToDate);
    }

    [Fact]
    public void Query_AllowsPreviousUnfinishedSamplesToBeExcludedExplicitly()
    {
        var query = new GetSampleRequestSampleTrialsQuery
        {
            SampleRequestCreatedToDate = new DateOnly(2026, 7, 31),
            IncludePreviousUnfinished = false
        };

        Assert.False(query.IncludePreviousUnfinished);
    }

    [Fact]
    public void ReportDto_SerializesStatusAsStableStringCode()
    {
        var json = JsonSerializer.Serialize(new SampleRequestSampleTrialReportDto
        {
            Status = SampleTrialStatus.WaitingCustomerFeedback
        });

        Assert.Contains("\"Status\":\"WaitingCustomerFeedback\"", json);
    }

    [Fact]
    public void ReportDto_AllowsMissingTrial()
    {
        var dto = new SampleRequestSampleTrialReportDto
        {
            SampleRequestId = Guid.NewGuid(),
            HasTrial = false,
            Status = null,
            TrialNo = null,
            SampleRequestStatus = "New"
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.Contains("\"HasTrial\":false", json);
        Assert.Contains("\"Status\":null", json);
        Assert.Contains("\"SampleRequestStatus\":\"New\"", json);
    }
}
