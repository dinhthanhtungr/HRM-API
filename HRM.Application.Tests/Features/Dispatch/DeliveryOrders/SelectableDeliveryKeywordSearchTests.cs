using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class SelectableDeliveryKeywordSearchTests
{
    [Theory]
    [InlineData("dhg261000170", false, false)]
    [InlineData("tp_123", true, false)]
    [InlineData("SK123", true, true)]
    [InlineData("123", true, true)]
    public void SearchBranches_TranslateToSqlAndOnlyIncludeExpectedSearchFields(
        string keyword, bool searchesSamples, bool searchesColour)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var companyId = Guid.NewGuid();
        var query = db.MerchandiseOrderDetails.AsNoTracking()
            .Where(x => x.IsActive && x.MerchandiseOrder.CompanyId == companyId)
            .Select(x => new SelectableDeliveryLineSource
            {
                Order = x.MerchandiseOrder, Detail = x,
                DeliveredQuantity = 0m, RemainingQuantity = x.ExpectedQuantity
            });

        var sql = SelectableDeliveryKeywordSearch.Apply(query, keyword, companyId)
            .Select(x => x.Detail.MerchandiseOrderDetailId).ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Equal(searchesSamples, sql.Contains("SampleRequests", StringComparison.Ordinal));
        Assert.Equal(searchesColour, sql.Contains("ColourCode", StringComparison.Ordinal));
    }
}
