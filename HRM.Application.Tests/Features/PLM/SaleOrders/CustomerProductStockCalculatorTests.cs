using HRM.Application.Features.PLM.SaleOrders.Queries.GetCustomerProductStock;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class CustomerProductStockCalculatorTests
{
    [Fact]
    public void Calculate_GroupsShelvesAndSubtractsLotReservation()
    {
        var formulaId = Guid.NewGuid();
        var mfgId = Guid.NewGuid();
        var result = CustomerProductStockCalculator.Calculate(
            [new(formulaId, "VA26080001", mfgId, "MFG26080001", new DateTime(2026, 8, 1))],
            new Dictionary<Guid, int> { [formulaId] = 1 },
            [
                new(1, "TP.1", "VA26080001", "BATCH-01", 60m),
                new(2, "TP.2", "VA26080001", "BATCH-01", 40m),
                new(3, "TP.3", "VA-OTHER", "BATCH-02", 100m)
            ],
            [new("BATCH-01", 25m), new(null, 10m)]);

        Assert.Equal(100m, result.TotalOnHandKg);
        Assert.Equal(25m, result.ReservedOpenKg);
        Assert.Equal(75m, result.AvailableKg);
        Assert.Equal(165m, result.ProductAvailableKg);
        var lot = Assert.Single(result.Lots);
        Assert.Equal(2, lot.Shelves.Count);
        Assert.Equal(mfgId, Assert.Single(lot.SourceMfgOrders).MfgProductionOrderId);
    }

    [Fact]
    public void Calculate_UsesLotKeyWhenLegacyStockHasNoLotNo()
    {
        var formulaId = Guid.NewGuid();
        var result = CustomerProductStockCalculator.Calculate(
            [new(formulaId, "VA26080002", Guid.NewGuid(), "MFG26080002", DateTime.Now)],
            new Dictionary<Guid, int> { [formulaId] = 1 },
            [new(1, "TP.1", null, "VA26080002", 50m)],
            [new("VA26080002", 5m)]);

        var lot = Assert.Single(result.Lots);
        Assert.Equal(50m, lot.OnHandKg);
        Assert.Equal(5m, lot.ReservedOpenKg);
        Assert.Equal(45m, lot.AvailableKg);
    }

    [Fact]
    public void Calculate_SeparatesFormulaUsedByMultipleCustomersAsAmbiguous()
    {
        var formulaId = Guid.NewGuid();
        var result = CustomerProductStockCalculator.Calculate(
            [new(formulaId, "VA-SHARED", Guid.NewGuid(), "MFG26080003", DateTime.Now)],
            new Dictionary<Guid, int> { [formulaId] = 2 },
            [new(1, "TP.1", "VA-SHARED", "BATCH-03", 80m)],
            []);

        Assert.Equal(0m, result.TotalOnHandKg);
        Assert.Equal(0m, result.AvailableKg);
        Assert.Equal(80m, result.AmbiguousOnHandKg);
        Assert.True(Assert.Single(result.Lots).IsAttributionAmbiguous);
    }
}
