using HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ProductionTrackingTests
{
    [Fact]
    public void TrackingProjectionReadsTotalsWithoutFormulaOrPrice()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = ProductionTrackingReader.Project(db.MfgProductionOrders.Take(25)).ToQueryString();
        Assert.Contains("total_quantity", sql);
        Assert.DoesNotContain("formula_id", sql);
        Assert.DoesNotContain("unit_price_agreed", sql);
        Assert.DoesNotContain("num_of_batches", sql);
        Assert.DoesNotContain("lab_note", sql);
    }
}
