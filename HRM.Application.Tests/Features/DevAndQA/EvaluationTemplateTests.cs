using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class EvaluationTemplateTests
{
    private readonly Guid companyId = Guid.NewGuid();

    private EvaluationTemplate Template(EvaluationTemplateType type = EvaluationTemplateType.Iqc)
    {
        var template = new EvaluationTemplate
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Type = type,
            Code = "DEFAULT", Name = "Mẫu chung", Version = 2,
            Status = EvaluationTemplateStatus.Published
        };
        template.Items.Add(new EvaluationTemplateItem
        {
            Id = Guid.NewGuid(), EvaluationTemplateId = template.Id,
            EvaluationStandardId = Guid.NewGuid(), CriterionCode = "MOISTURE", CriterionName = "Độ ẩm",
            DataType = EvaluationDataType.Decimal, Unit = "%", TestMethod = "Sấy",
            AcceptanceRuleJson = "{\"operator\":\"LessThanOrEqual\",\"value\":0.2}", IsRequired = true
        });
        return template;
    }

    [Fact]
    public void IqcCopiesConfigurationAndTemplateChangesDoNotAffectSavedRow()
    {
        var template = Template();
        var evaluation = EvaluationSnapshotFactory.CreateIqc(template, Guid.NewGuid(), companyId);
        var row = Assert.Single(evaluation.Results);
        var rule = row.AcceptanceRuleJson;
        template.Items.Single().AcceptanceRuleJson = "{}";
        template.Items.Single().CriterionName = "Tên mới";
        Assert.Equal(rule, row.AcceptanceRuleJson);
        Assert.Equal("Độ ẩm", row.CriterionName);
        Assert.Equal(2, evaluation.TemplateVersion);
        Assert.True(row.HasConfigurationSnapshot);
        Assert.Null(row.Value);
        Assert.Null(row.IqcSpecificationItemId);
        Assert.Null(evaluation.IqcSpecificationId);
        Assert.Equal(evaluation.Id, row.IqcInspectionEvaluationId);
    }

    [Fact]
    public void AdditionalRowUsesCatalogWithoutTemplateOrLegacySpecification()
    {
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = companyId, Code = "COLOR", Name = "Màu sắc" };
        var row = EvaluationSnapshotFactory.CreateAdditionalIqc(standard, Guid.NewGuid(), companyId);
        standard.Name = "Đổi tên";
        Assert.Equal("Màu sắc", row.CriterionName);
        Assert.Equal(standard.Id, row.EvaluationStandardId);
        Assert.Null(row.SourceTemplateItemId);
        Assert.Null(row.IqcSpecificationItemId);
        Assert.True(row.HasConfigurationSnapshot);
    }

    [Fact]
    public void CoaCopiesTemplateAndCannotReinitializeExistingCertificate()
    {
        var certificate = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = companyId };
        var template = Template(EvaluationTemplateType.Coa);
        EvaluationSnapshotFactory.InitializeCoa(certificate, template);
        var row = Assert.Single(certificate.Results);
        Assert.Equal(certificate.Id, row.CoaCertificateId);
        Assert.Equal(template.Items.Single().Id, row.SourceTemplateItemId);
        Assert.Null(certificate.CoaSpecificationId);
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.InitializeCoa(certificate, template));
    }

    [Fact]
    public void TemplateRejectsOtherCompanyWrongTypeAndDraft()
    {
        var template = Template();
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateIqc(template, Guid.NewGuid(), Guid.NewGuid()));
        template.Type = EvaluationTemplateType.Coa;
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateIqc(template, Guid.NewGuid(), companyId));
        template.Type = EvaluationTemplateType.Iqc;
        template.Status = EvaluationTemplateStatus.Draft;
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateIqc(template, Guid.NewGuid(), companyId));
    }

    [Fact]
    public void MethodSnapshotIsIndependentAndRejectsOtherCompany()
    {
        var method = new MeasurementMethod { Id = Guid.NewGuid(), CompanyId = companyId, Code = "DRY", Name = "Sấy" };
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = companyId, Code = "MOISTURE", Name = "Độ ẩm", MeasurementMethodId = method.Id };
        var row = EvaluationSnapshotFactory.CreateAdditionalCoa(standard, Guid.NewGuid(), companyId, method);
        method.Name = "Tên mới";
        Assert.Equal("Sấy", row.TestMethod);
        Assert.Equal("DRY", row.MeasurementMethodCode);
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateAdditionalCoa(standard, Guid.NewGuid(), companyId));
        method.CompanyId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateAdditionalCoa(standard, Guid.NewGuid(), companyId, method));
    }

    [Fact]
    public void PublishedTemplateCannotBeEditedThroughFactory()
    {
        var standard = new EvaluationStandard { CompanyId = companyId };
        Assert.Throws<InvalidOperationException>(() => EvaluationSnapshotFactory.CreateTemplateItem(standard, Template()));
    }

    [Fact]
    public void EfModelHasNoShadowColumnsAndAllowsAdditionalRows()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        foreach (var type in new[] { typeof(MeasurementMethod), typeof(EvaluationTemplate), typeof(EvaluationTemplateItem), typeof(IqcInspectionResult), typeof(CoaCertificateResult) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.DoesNotContain(entity.GetProperties(), x => x.IsShadowProperty());
            Assert.All(entity.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        }
        Assert.True(db.Model.FindEntityType(typeof(IqcInspectionResult))!.FindProperty("IqcSpecificationItemId")!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(CoaCertificateResult))!.FindProperty("CoaSpecificationItemId")!.IsNullable);
        Assert.Contains(db.Model.FindEntityType(typeof(EvaluationTemplate))!.GetIndexes(), x => x.IsUnique && x.GetFilter() == "isdefault = TRUE AND status = 1");
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("hasconfigurationsnapshot", sql);
        Assert.Contains("ck_iqcinspectionresult_snapshot", sql);
        Assert.Contains("MeasurementMethod", sql);
    }
}
