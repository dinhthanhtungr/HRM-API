using HRM.Domain.Entities.DevandqaSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class EvaluationStandardPersistenceModelTests
{
    [Theory]
    [InlineData(typeof(IqcInspectionResult))]
    [InlineData(typeof(CoaCertificateResult))]
    public void TemplateSourceIdIsMappedWithoutForeignKey(Type entityType)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(entityType)!;
        var sourceId = entity.FindProperty("SourceTemplateItemId");
        Assert.NotNull(sourceId);
        Assert.True(sourceId.IsNullable);
        Assert.False(sourceId.IsShadowProperty());
        Assert.DoesNotContain(entity.GetForeignKeys(), fk => fk.Properties.Contains(sourceId));
        Assert.Null(entity.FindNavigation("SourceTemplateItem"));
    }

    [Fact]
    public void Relationships_UseExistingForeignKeysWithoutShadowColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(typeof(EvaluationStandard))!;

        Assert.DoesNotContain(entity.GetProperties(), property => property.IsShadowProperty());
        Assert.Equal(4, entity.GetForeignKeys().Count());
        foreach (var (navigation, foreignKey, inverse, required) in new[]
        {
            ("Company", "CompanyId", "EvaluationStandards", true),
            ("CreatedByEmployee", "CreatedBy", "EvaluationStandardCreatedByNavigations", true),
            ("UpdatedByEmployee", "UpdatedBy", "EvaluationStandardUpdatedByNavigations", false)
        })
        {
            var relationship = entity.FindNavigation(navigation)!.ForeignKey;
            Assert.Equal(foreignKey, Assert.Single(relationship.Properties).Name);
            Assert.Equal(inverse, relationship.PrincipalToDependent!.Name);
            Assert.Equal(required, relationship.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        }
    }

    [Theory]
    [InlineData(typeof(IqcInspectionResult))]
    [InlineData(typeof(CoaCertificateResult))]
    public void AttachmentCollectionIsOptionalAndDoesNotCascadeDelete(Type entityType)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(entityType)!;
        var property = entity.FindProperty("AttachmentCollectionId")!;
        Assert.True(property.IsNullable);
        Assert.False(property.IsShadowProperty());
        var foreignKey = entity.FindNavigation("AttachmentCollection")!.ForeignKey;
        Assert.Equal(property, Assert.Single(foreignKey.Properties));
        Assert.False(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Theory]
    [InlineData(typeof(EvaluationStandard))]
    [InlineData(typeof(EvaluationTemplateItem))]
    [InlineData(typeof(IqcSpecificationItem))]
    [InlineData(typeof(CoaSpecificationItem))]
    public void ConfigurationOnlyEntitiesDoNotStoreFiles(Type entityType)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(entityType)!;
        Assert.Null(entity.FindProperty("AttachmentCollectionId"));
        Assert.Null(entity.FindNavigation("AttachmentCollection"));
        Assert.NotNull(entity.FindProperty("DataType"));
        Assert.NotNull(entity.FindProperty("InputConfigJson"));
    }

}
