using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;
using System.Text.Json;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSampleTrialReportRulesTests
{
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
        Assert.Null(SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(null, DateTime.UtcNow));
        Assert.Null(SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(DateTime.UtcNow, null));
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
