using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaItemStockCodeTests
{
    [Theory]
    [InlineData(ItemType.Product)]
    [InlineData(ItemType.ProductFailure)]
    public void Product_UsesColourCodeWhileKeepingSampleRequestDisplayCode(ItemType type)
    {
        var id = Guid.NewGuid();
        var source = new FormulaItemDisplaySource(id, type, "Old", "TP_OLD");
        var data = new Dictionary<FormulaItemDisplayKey, FormulaItemCurrentData>
        {
            [new(false, id)] = new("Product", "TP_13197", "LI61094C")
        };
        var result = FormulaItemDisplayResolver.Resolve(source, data);
        Assert.Equal("TP_13197", result.ExternalId);
        Assert.Equal("LI61094C", result.StockCode);
        Assert.Null(FormulaItemDisplayResolver.Resolve(source, new Dictionary<FormulaItemDisplayKey, FormulaItemCurrentData>()).StockCode);
    }

    [Theory]
    [InlineData(ItemType.Material)]
    [InlineData(ItemType.MaterialFailure)]
    public void Material_UsesMaterialExternalId(ItemType type)
    {
        var id = Guid.NewGuid();
        var result = FormulaItemDisplayResolver.Resolve(new(id, type, "Material", "NVL_OLD"),
            new Dictionary<FormulaItemDisplayKey, FormulaItemCurrentData> { [new(true, id)] = new("Material", "NVL_NEW", null) });
        Assert.Equal("NVL_NEW", result.StockCode);
    }
}
