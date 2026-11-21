using HRM.Domain.Entities.DevandqaSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class LabTestPersistenceModelTests
{
    [Fact]
    public void IndependentTestsUseExplicitKeysAndSnapshotWithoutVoucher()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var header = model.FindEntityType(typeof(LabTest))!;
        var result = model.FindEntityType(typeof(LabTestResult))!;
        Assert.Null(header.FindProperty("VoucherDetailId"));
        Assert.True(header.FindProperty(nameof(LabTest.MaterialId))!.IsNullable);
        Assert.Equal("materialid", header.FindProperty(nameof(LabTest.MaterialId))!.GetColumnName());
        Assert.Equal("materialcode", header.FindProperty(nameof(LabTest.MaterialCode))!.GetColumnName());
        var materialFk = header.FindNavigation(nameof(LabTest.Material))!.ForeignKey;
        Assert.Equal(typeof(HRM.Domain.Entities.MaterialSchema.Material), materialFk.PrincipalEntityType.ClrType);
        Assert.False(materialFk.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, materialFk.DeleteBehavior);
        Assert.True(header.FindProperty(nameof(LabTest.SupplierId))!.IsNullable);
        Assert.Equal("supplierid", header.FindProperty(nameof(LabTest.SupplierId))!.GetColumnName());
        var supplierFk = header.FindNavigation(nameof(LabTest.Supplier))!.ForeignKey;
        Assert.Equal(typeof(HRM.Domain.Entities.MaterialSchema.Supplier), supplierFk.PrincipalEntityType.ClrType);
        Assert.False(supplierFk.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, supplierFk.DeleteBehavior);
        Assert.Equal("batchnumber", header.FindProperty(nameof(LabTest.BatchNumber))!.GetColumnName());
        Assert.Equal("weightkg", header.FindProperty(nameof(LabTest.WeightKg))!.GetColumnName());
        Assert.Equal(18, header.FindProperty(nameof(LabTest.WeightKg))!.GetPrecision());
        Assert.Equal(6, header.FindProperty(nameof(LabTest.WeightKg))!.GetScale());
        Assert.Null(result.FindProperty("ObservedText"));
        Assert.Equal("value", result.FindProperty(nameof(LabTestResult.Value))!.GetColumnName());
        var sql = db.Set<LabTest>().Include(x => x.Results).Take(5).ToQueryString();
        Assert.DoesNotContain("observedtext", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(header.GetProperties(), x => x.IsShadowProperty());
        Assert.DoesNotContain(result.GetProperties(), x => x.IsShadowProperty());
        Assert.Contains(header.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CompanyId", "Code"]));
        Assert.DoesNotContain(result.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "SourceTemplateItemId"));
        Assert.Equal(DeleteBehavior.Restrict, result.FindNavigation("AttachmentCollection")!.ForeignKey.DeleteBehavior);
        Assert.Equal("Results", result.FindNavigation("LabTest")!.ForeignKey.PrincipalToDependent!.Name);
        Assert.Contains(result.GetCheckConstraints(), x => x.Sql.Contains("BETWEEN 1 AND 8"));
    }
}

