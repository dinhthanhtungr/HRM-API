using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaVersionServiceTests
{
    [Fact]
    public void CreateSnapshot_CopiesHeaderItemsAndCalculatedTotal()
    {
        var formula = CreateFormula();
        var material = CreateMaterial(formula, lineNo: 1, quantity: 2.5m, unitPrice: 4m);
        var now = new DateTime(2026, 8, 14, 10, 30, 0);
        var employeeId = Guid.NewGuid();

        var version = FormulaVersionService.CreateSnapshot(
            formula,
            [material],
            versionNo: 1,
            now,
            employeeId,
            "Initial version");

        Assert.Equal(1, version.VersionNo);
        Assert.Equal(formula.FormulaId, version.FormulaId);
        Assert.Equal(formula.Name, version.Name);
        Assert.Equal(formula.TotalPrice, version.TotalPrice);
        Assert.Equal(now, version.EffectiveFrom);
        Assert.Null(version.EffectiveTo);
        Assert.Equal(employeeId, version.CreatedBy);

        var item = Assert.Single(version.Items);
        Assert.Equal(1, item.LineNo);
        Assert.Equal(10m, item.TotalPrice);
        Assert.Equal(material.MaterialExternalIdSnapshot, item.MaterialExternalIdSnapshot);
        Assert.Equal(material.MaterialNameSnapshot, item.MaterialNameSnapshot);
    }

    [Fact]
    public void CreateSnapshot_RemainsUnchangedWhenSourceMaterialChanges()
    {
        var formula = CreateFormula();
        var material = CreateMaterial(formula, lineNo: 1, quantity: 1m, unitPrice: 5m);
        var version = FormulaVersionService.CreateSnapshot(
            formula,
            [material],
            versionNo: 1,
            DateTime.Now,
            Guid.NewGuid(),
            null);

        material.MaterialExternalIdSnapshot = "NEW-CODE";
        material.MaterialNameSnapshot = "New name";
        material.UnitPrice = 99m;

        var item = Assert.Single(version.Items);
        Assert.Equal("MAT-OLD", item.MaterialExternalIdSnapshot);
        Assert.Equal("Old material", item.MaterialNameSnapshot);
        Assert.Equal(5m, item.UnitPrice);
    }

    [Fact]
    public void CreateSnapshot_AllowsEachFormulaToStartAtVersionOne()
    {
        var first = FormulaVersionService.CreateSnapshot(
            CreateFormula(), [], 1, DateTime.Now, Guid.NewGuid(), null);
        var second = FormulaVersionService.CreateSnapshot(
            CreateFormula(), [], 1, DateTime.Now, Guid.NewGuid(), null);

        Assert.NotEqual(first.FormulaId, second.FormulaId);
        Assert.Equal(1, first.VersionNo);
        Assert.Equal(1, second.VersionNo);
    }

    [Fact]
    public void GetNextVersionNo_ReturnsOneThenTwo()
    {
        Assert.Equal(1, FormulaVersionService.GetNextVersionNo(null));
        Assert.Equal(
            2,
            FormulaVersionService.GetNextVersionNo(new FormulaVersion { VersionNo = 1 }));
    }

    [Fact]
    public void CloseOpenVersions_LeavesOnlyNewSnapshotCurrent()
    {
        var effectiveTo = new DateTime(2026, 8, 14, 11, 0, 0);
        var oldVersions = new[]
        {
            new FormulaVersion { VersionNo = 1 },
            new FormulaVersion { VersionNo = 2 }
        };

        FormulaVersionService.CloseOpenVersions(oldVersions, effectiveTo);
        var current = FormulaVersionService.CreateSnapshot(
            CreateFormula(), [], 3, effectiveTo, Guid.NewGuid(), null);

        Assert.All(oldVersions, version => Assert.Equal(effectiveTo, version.EffectiveTo));
        Assert.Null(current.EffectiveTo);
        Assert.Single(oldVersions.Append(current), version => version.EffectiveTo == null);
    }

    [Fact]
    public void SnapshotMatches_DetectsHeaderOrItemChanges()
    {
        var formula = CreateFormula();
        var material = CreateMaterial(formula, lineNo: 1, quantity: 1m, unitPrice: 5m);
        var version = FormulaVersionService.CreateSnapshot(
            formula, [material], 1, DateTime.Now, Guid.NewGuid(), null);

        Assert.True(FormulaVersionService.SnapshotMatches(version, formula, [material]));

        material.Quantity = 2m;

        Assert.False(FormulaVersionService.SnapshotMatches(version, formula, [material]));
    }

    [Fact]
    public void EfModel_ConfiguresRequiredKeysIndexesPrecisionAndDeleteBehaviors()
    {
        using var dbContext = CreateDbContext();
        var version = dbContext.Model.FindEntityType(typeof(FormulaVersion))!;
        var item = dbContext.Model.FindEntityType(typeof(FormulaVersionItem))!;

        Assert.Equal("FormulaVersions", version.GetTableName());
        Assert.Equal("SampleRequests", version.GetSchema());
        Assert.Equal(
            ValueGenerated.Never,
            version.FindProperty(nameof(FormulaVersion.FormulaVersionId))!.ValueGenerated);
        Assert.Equal(22, version.FindProperty(nameof(FormulaVersion.TotalPrice))!.GetPrecision());
        Assert.Equal(6, version.FindProperty(nameof(FormulaVersion.TotalPrice))!.GetScale());
        Assert.Contains(version.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_FormulaVersions_FormulaId_VersionNo");
        Assert.Contains(version.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_FormulaVersions_FormulaId_Period");
        Assert.Contains(version.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_FormulaVersions_CreatedBy");

        Assert.Equal(
            ValueGenerated.Never,
            item.FindProperty(nameof(FormulaVersionItem.FormulaVersionItemId))!.ValueGenerated);
        Assert.Equal(12, item.FindProperty(nameof(FormulaVersionItem.Quantity))!.GetPrecision());
        Assert.Equal(10, item.FindProperty(nameof(FormulaVersionItem.Quantity))!.GetScale());
        Assert.Equal(22, item.FindProperty(nameof(FormulaVersionItem.UnitPrice))!.GetPrecision());
        Assert.Equal(6, item.FindProperty(nameof(FormulaVersionItem.UnitPrice))!.GetScale());
        Assert.Equal(22, item.FindProperty(nameof(FormulaVersionItem.TotalPrice))!.GetPrecision());
        Assert.Equal(6, item.FindProperty(nameof(FormulaVersionItem.TotalPrice))!.GetScale());
        Assert.Contains(item.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_FormulaVersionItems_VersionId_LineNo");
        Assert.Contains(item.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_FormulaVersionItems_MaterialId");
        Assert.Contains(item.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_FormulaVersionItems_ProductId");

        AssertDeleteBehavior(version, typeof(Formula), DeleteBehavior.Cascade);
        AssertDeleteBehavior(version, typeof(Employee), DeleteBehavior.SetNull);
        AssertDeleteBehavior(item, typeof(FormulaVersion), DeleteBehavior.Cascade);
        AssertDeleteBehavior(item, typeof(Material), DeleteBehavior.Restrict);
        AssertDeleteBehavior(item, typeof(Product), DeleteBehavior.Restrict);
        AssertDeleteBehavior(item, typeof(Category), DeleteBehavior.Restrict);
    }

    private static void AssertDeleteBehavior(
        IReadOnlyEntityType entityType,
        Type principalType,
        DeleteBehavior expected)
    {
        var foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            x => x.PrincipalEntityType.ClrType == principalType);
        Assert.Equal(expected, foreignKey.DeleteBehavior);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Formula CreateFormula()
    {
        return new Formula
        {
            FormulaId = Guid.NewGuid(),
            Name = "Formula A",
            Status = "Draft",
            Note = "Snapshot note",
            TotalPrice = 5m,
            ProductionPrice = 6m,
            PresidentPrice = 7m,
            ProfitMarginPrice = 2m
        };
    }

    private static FormulaMaterial CreateMaterial(
        Formula formula,
        int lineNo,
        decimal quantity,
        decimal unitPrice)
    {
        return new FormulaMaterial
        {
            FormulaMaterialId = Guid.NewGuid(),
            FormulaId = formula.FormulaId,
            LineNo = lineNo,
            itemType = ItemType.Material,
            MaterialId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = quantity * unitPrice,
            Unit = "kg",
            MaterialExternalIdSnapshot = "MAT-OLD",
            MaterialNameSnapshot = "Old material",
            IsActive = true
        };
    }
}
