using HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReports;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class GetComplaintReportsQueryTests
{
    [Fact]
    public void Pagination_NormalizesInvalidValuesAndKeyword()
    {
        var query = new GetComplaintReportsQuery
        {
            PageNumber = 0,
            PageSize = 500,
            Keyword = "  CAPA-001  "
        };

        Assert.Equal(1, query.NormalizedPageNumber);
        Assert.Equal(100, query.NormalizedPageSize);
        Assert.Equal("CAPA-001", query.NormalizedKeyword);
    }
}
