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
        Assert.DoesNotContain(header.GetProperties(), x => x.IsShadowProperty());
        Assert.DoesNotContain(result.GetProperties(), x => x.IsShadowProperty());
        Assert.Contains(header.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CompanyId", "Code"]));
        Assert.DoesNotContain(result.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "SourceTemplateItemId"));
        Assert.Equal(DeleteBehavior.Restrict, result.FindNavigation("AttachmentCollection")!.ForeignKey.DeleteBehavior);
        Assert.Equal("Results", result.FindNavigation("LabTest")!.ForeignKey.PrincipalToDependent!.Name);
        Assert.Contains(result.GetCheckConstraints(), x => x.Sql.Contains("BETWEEN 1 AND 8"));
    }
}

