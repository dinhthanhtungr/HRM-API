using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class BomRulesTests
{
    [Fact]
    public void ValidateCreateRequest_AcceptsMaterialAndProductItems()
    {
        var request = CreateValidRequest([
            new BomItemWriteDto { ItemId = Guid.NewGuid(), ItemType = ItemType.Material, Quantity = 1, Unit = "kg" },
            new BomItemWriteDto { ItemId = Guid.NewGuid(), ItemType = ItemType.Product, Quantity = 2, Unit = "kg" }
        ]);

        Assert.Null(BomRules.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_RejectsFailureItemTypes()
    {
        var request = CreateValidRequest([
            new BomItemWriteDto { ItemId = Guid.NewGuid(), ItemType = ItemType.MaterialFailure, Quantity = 1, Unit = "kg" }
        ]);

        Assert.Equal("E-BOM items must be Material or Product.", BomRules.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidatePeriod_RejectsEqualOrReversedRange()
    {
        var start = new DateTime(2026, 9, 8);

        Assert.NotNull(BomRules.ValidatePeriod(start, start));
        Assert.NotNull(BomRules.ValidatePeriod(start, start.AddDays(-1)));
        Assert.Null(BomRules.ValidatePeriod(start, start.AddDays(1)));
    }

    private static CreateBomRequest CreateValidRequest(IReadOnlyList<BomItemWriteDto> items)
        => new()
        {
            ProductId = Guid.NewGuid(),
            Code = "EBOM-001",
            Name = "Engineering BOM",
            BaseOutputQuantity = 1,
            OutputUnit = "kg",
            Items = items
        };
}
