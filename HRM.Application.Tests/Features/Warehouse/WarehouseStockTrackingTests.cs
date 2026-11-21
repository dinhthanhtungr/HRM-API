using HRM.Application.Features.Warehouse.Queries.GetWarehouseStockTracking;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class WarehouseStockTrackingTests
{
    [Fact]
    public void StockTrackingKeywordAndPagingTranslateWithoutLoadingLinksOrLots()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = GetWarehouseStockTrackingQueryHandler.BuildRows(db, Guid.NewGuid())
            .Where(row => row.Code.ToLower().Contains("abc") ||
                (row.Name != null && row.Name.ToLower().Contains("abc")))
            .OrderBy(row => row.Code).ThenBy(row => row.StockType)
            .Skip(25).Take(25).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("VaCode", sql);
        Assert.DoesNotContain("LotKey", sql);
        Assert.DoesNotContain("Formula", sql);
    }
}
