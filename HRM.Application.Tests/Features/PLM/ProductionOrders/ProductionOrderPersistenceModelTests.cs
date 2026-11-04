using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

public sealed class ProductionOrderPersistenceModelTests
{
    [Fact]
    public void FormulaGraph_TracksAllMaterialsOnExistingRelationalModel()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        IProductionOrderDbContext abstraction = db;
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 5, 12, 0, 0);
        var formula = ProductionOrderFormulaFactory.Create(new CreateProductionOrderInformRequest
        {
            FormulaItems =
            [
                new() { ItemId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), ItemType = ItemType.Material, Quantity = .25m },
                new() { ItemId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), ItemType = ItemType.Product, Quantity = .75m }
            ]
        }, companyId, employeeId, now, "VA261000001");
        abstraction.ManufacturingFormulas.Add(formula);
        Assert.Equal(EntityState.Added, db.Entry(formula).State);
        var materials = db.ChangeTracker.Entries<ManufacturingFormulaMaterial>().ToArray();
        Assert.Equal(2, materials.Length);
        Assert.All(materials, row =>
        {
            Assert.Equal(EntityState.Added, row.State);
            Assert.Equal(formula.ManufacturingFormulaId, row.Entity.ManufacturingFormulaId);
        });
        Assert.Same(db.WarehouseTempStocks, abstraction.WarehouseTempStocks);
        var model = db.Model.FindEntityType(typeof(ManufacturingFormulaMaterial))!;
        Assert.Equal("manufacturing", model.GetSchema());
        Assert.Equal(18, db.Model.FindEntityType(typeof(MfgProductionOrder))!
            .FindProperty(nameof(MfgProductionOrder.TotalQuantityRequest))!.GetPrecision());
    }
}
