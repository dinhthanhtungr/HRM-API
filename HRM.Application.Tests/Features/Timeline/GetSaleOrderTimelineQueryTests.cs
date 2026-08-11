using HRM.Application.Features.Timeline.Queries.GetSaleOrderTimeline;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Tests.Features.Timeline;

public sealed class GetSaleOrderTimelineQueryTests
{
    [Fact]
    public void ComplaintFilters_PreserveExplicitValues()
    {
        var query = new GetSaleOrderTimelineQuery
        {
            HasComplaint = true,
            ComplaintStatus = ComplaintReportStatus.Investigating
        };

        Assert.True(query.HasComplaint);
        Assert.Equal(ComplaintReportStatus.Investigating, query.ComplaintStatus);
    }

    [Fact]
    public void ComplaintFilters_DefaultToNoFiltering()
    {
        var query = new GetSaleOrderTimelineQuery();

        Assert.Null(query.HasComplaint);
        Assert.Null(query.ComplaintStatus);
    }
}
