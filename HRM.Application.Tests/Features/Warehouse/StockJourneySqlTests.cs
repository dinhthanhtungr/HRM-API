using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockJourneySqlTests
{
    [Fact]
    public void CurrentLocationsAndVerifiedProductionLinksTranslateWithoutReadingCosts()
    {
        // Generates SQL only: no connection is opened and no schema/data setup runs.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var company = Guid.NewGuid();
        var locations = StockJourneyCurrentReader.LocationsQuery(db, company, "NVL-1", "LOT-1", false).ToQueryString();
        var history = db.WarehouseMovementRows.Where(x => x.CompanyId == company && x.ProductCode == "NVL-1" && x.LotNumber == "LOT-1");
        var allocations = StockJourneyCurrentReader.AllocationsQuery(db, history, company, "NVL-1", "LOT-1").ToQueryString();
        Assert.Contains("WarehouseShelfStock", locations);
        Assert.Contains("isActive", locations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OperationMaterialBuffer", allocations);
        Assert.Contains("VoucherDetailId", allocations, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AvgUnitCost", allocations);
        Assert.DoesNotContain("AmountCost", allocations);
        Assert.DoesNotContain("UncostedReason", allocations);
    }
}
