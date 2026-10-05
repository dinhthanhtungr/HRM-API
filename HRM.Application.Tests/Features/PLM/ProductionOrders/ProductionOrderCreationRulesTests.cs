using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

public sealed class ProductionOrderCreationRulesTests
{
    [Theory]
    [InlineData("1.0001", true)]
    [InlineData("0.9999", true)]
    [InlineData("1.00011", false)]
    [InlineData("0.99989", false)]
    public void FormulaRatio_PreservesLegacyTolerance(string ratio, bool accepted)
    {
        var request = new CreateProductionOrderInformRequest
        {
            MerchandiseOrderId = Guid.NewGuid(), MerchandiseOrderDetailId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(), RequiredDate = new(2026, 10, 5),
            FormulaItems = [new() { Quantity = decimal.Parse(ratio, System.Globalization.CultureInfo.InvariantCulture) }]
        };
        Assert.Equal(accepted, ProductionOrderCreationRules.ValidateInform(request) is null);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData(" n/A ", null)]
    [InlineData(" LOT-1 ", "LOT-1")]
    public void LotNormalization(string? input, string? expected)
        => Assert.Equal(expected, ProductionOrderCreationRules.NormalizeLotNumber(new() { LotNo = input! }));

    [Theory]
    [InlineData(ItemType.Material, StockType.DefectiveRawMaterial, ItemType.MaterialFailure)]
    [InlineData(ItemType.MaterialFailure, StockType.RawMaterial, ItemType.Material)]
    [InlineData(ItemType.Product, StockType.DefectiveFinishedGood, ItemType.ProductFailure)]
    [InlineData(ItemType.ProductFailure, StockType.FinishedGood, ItemType.Product)]
    public void ItemType_UsesStockTypeRatherThanIsDefective(ItemType input, StockType stockType, ItemType expected)
        => Assert.Equal(expected, ProductionOrderCreationRules.ResolveItemType(input,
            new() { StockType = stockType, IsDefective = false }));

    [Fact]
    public void Reservation_GroupsCodesAndUsesCurrentProductColourCode()
    {
        var productId = Guid.NewGuid();
        var target = ProductionOrderReservationService.BuildTargets(
        [
            new() { ItemType = ItemType.Material, Quantity = .2m, MaterialExternalIdSnapshot = " m01 " },
            new() { ItemType = ItemType.MaterialFailure, Quantity = .3m, MaterialExternalIdSnapshot = "M01" },
            new() { ItemType = ItemType.ProductFailure, ItemId = productId, Quantity = .5m, MaterialExternalIdSnapshot = "WRONG" },
            new() { ItemType = ItemType.Material, Quantity = 1m, IsActive = false, MaterialExternalIdSnapshot = "IGNORE" }
        ], new Dictionary<Guid, string> { [productId] = " p01 " }, 100);
        Assert.Equal(2, target.Count);
        Assert.Equal(50m, target["M01"]);
        Assert.Equal(50m, target["P01"]);
    }

    [Fact]
    public void Reservation_PreservesDuplicateRowsAndAllowsBelowUsed_CancelsRemovedCodes()
    {
        var rows = new[]
        {
            new WarehouseTempStock { TempId = 1, Code = "M01", LotKey = "LOT", QtyRequest = 20, QtyUsed = 10 },
            new WarehouseTempStock { TempId = 2, Code = " m01 ", QtyRequest = 20, QtyUsed = 10 },
            new WarehouseTempStock { TempId = 3, Code = "REMOVED", QtyRequest = 30, QtyUsed = 15 }
        };
        var added = new List<WarehouseTempStock>();
        var order = new MfgProductionOrder { ExternalId = "MFG261000001", CompanyId = Guid.NewGuid(), CreatedBy = Guid.NewGuid() };
        ProductionOrderReservationService.ApplyTargets(order,
            new Dictionary<string, decimal> { ["M01"] = 5, ["NEW"] = 12 }, rows, DateTime.Now, added.Add);
        Assert.Equal(20m, rows[0].QtyRequest);
        Assert.Equal(5m, rows[1].QtyRequest);
        Assert.Equal(10m, rows[1].QtyUsed);
        Assert.Equal("Open", rows[1].ReserveStatus);
        Assert.Equal("Cancelled", rows[2].ReserveStatus);
        Assert.Equal(0m, rows[2].QtyRequest);
        Assert.Equal(15m, rows[2].QtyUsed);
        Assert.Equal("NEW", Assert.Single(added).Code);
        Assert.Equal(order.ExternalId, added[0].VaCode);
    }
}
