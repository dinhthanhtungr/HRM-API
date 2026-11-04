using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingInformItemTrackingTests
{
    [Theory]
    [InlineData(false, EntityState.Modified)]
    [InlineData(true, EntityState.Added)]
    public void ReplacingMaterial_TracksNewRowAsInsert(bool explicitlyAdd, EntityState expected)
    {
        // Uses the production EF model without opening a database connection.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid() };
        var removed = new ManufacturingFormulaMaterial
        {
            ManufacturingFormulaMaterialId = Guid.NewGuid(),
            ManufacturingFormulaId = formula.ManufacturingFormulaId,
            MaterialId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), IsActive = true,
        };
        formula.ManufacturingFormulaMaterials.Add(removed);
        db.Attach(formula);
        var materialId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        Action<ManufacturingFormulaMaterial>? add = explicitlyAdd
            ? row => db.ManufacturingFormulaMaterials.Add(row) : null;
        ManufacturingFormulaItemWriter.Replace(formula,
            [new ManufacturingFormulaInputItem { ItemId = materialId, ItemType = ItemType.Material, Quantity = 0.04m }],
            [new CreateProductionOrderFormulaItemRequest { ItemId = materialId, ItemType = ItemType.Material,
                CategoryId = categoryId, Quantity = 0.04m, UnitPrice = 100m, LineNo = 1 }],
            trackNewItem: add);
        db.ChangeTracker.DetectChanges();
        var added = formula.ManufacturingFormulaMaterials.Single(x => x.MaterialId == materialId);
        Assert.Equal(expected, db.Entry(added).State);
        Assert.False(removed.IsActive);
        Assert.Equal(EntityState.Modified, db.Entry(removed).State);
        Assert.Equal(0.04m, added.Quantity);
    }
}
