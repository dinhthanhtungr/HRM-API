using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.LabTests.Queries.GetMaterialTestGrid;
using HRM.Application.Features.DevAndQA.LabTests.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA.LabTests;

public sealed class MaterialTestGridTests
{
    [Fact]
    public async Task GridKeepsBothSourcesAndExcludesDeletedForeignAndEmptyIqcRows()
    {
        var user = new IqcWorkflowTests.User { Roles = ["LabUser"] };
        var company = user.CompanyId!.Value;
        var (db, store) = IqcTestStore.CreateContext<ILabTestDbContext>();
        var service = new MaterialTestGridService(db, new EvaluationAccessService(db, user, new CurrentUserPermissionService(user)));
        var lab = new LabTest { Id = Guid.NewGuid(), CompanyId = company, Code = "NVL_TEST_00000001", SampleName = "Sample", TestDate = new(2026, 1, 1) };
        store.Seed("LabTests", lab, new() { CompanyId = company, IsDeleted = true }, new() { CompanyId = Guid.NewGuid() });
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), CreatedDate = new(2026, 1, 2), VoucherDetailId = 1,
            WarehouseVoucherDetail = new() { ProductCode = "MAT", ProductName = "PP", Voucher = new() { CompanyId = company } } };
        var evaluation = new IqcInspectionEvaluation { QCInputByQCId = qc.QCInputByQCId, QCInputByQC = qc,
            Results = [new() { Id = Guid.NewGuid(), HasConfigurationSnapshot = true, CriterionCode = "DENSITY", CriterionName = "Density", Value = "0", DataType = EvaluationDataType.Decimal }] };
        store.Seed("IqcInspectionEvaluations", evaluation, new() { QCInputByQC = qc });
        store.Seed("Materials", new Material { MaterialId = Guid.NewGuid(), CompanyId = company, ExternalId = "MAT" });
        var result = await service.ReadAsync(new(), default);
        Assert.Equal(2, result.Page.TotalCount);
        Assert.Equal(MaterialTestSource.Iqc, result.Page.Items[0].Source);
        Assert.Equal("MAT", result.Page.Items[0].MaterialCode);
        Assert.False(result.Page.Items[0].CanDelete);
        Assert.Equal("0", Assert.Single(result.Page.Items[0].Cells).Value);
        Assert.Equal(lab.Code, result.Page.Items[1].MaterialCode);
        Assert.True(result.Page.Items[1].CanDelete);
        Assert.Single(result.Columns);
    }

    [Fact]
    public async Task DeniedGridDoesNotQueryData()
    {
        var user = new IqcWorkflowTests.User { Roles = ["SaleUser"] };
        var service = new MaterialTestGridService(null!, new EvaluationAccessService(null!, user, new CurrentUserPermissionService(user)));
        Assert.Equal(403, (await Assert.ThrowsAsync<EvaluationRequestException>(() => service.ReadAsync(new(), default))).StatusCode);
    }

    [Fact]
    public void CombinedGridTranslatesToCompanyScopedPaginatedPostgresQuery()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new IqcWorkflowTests.User { Roles = ["LabUser"] };
        var service = new MaterialTestGridService(db, new EvaluationAccessService(db, user, new CurrentUserPermissionService(user)));
        var sql = service.BuildIndex(user.CompanyId!.Value, new GetMaterialTestGridQuery(Keyword: "PP", FromDate: new(2023, 1, 1), ToDate: new(2026, 1, 1)))
            .OrderByDescending(x => x.TestDate).ThenBy(x => x.Source).ThenByDescending(x => x.Id).Skip(30).Take(30).ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("ILIKE", sql);
        Assert.Contains("isdeleted", sql);
        Assert.Contains("companyid", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void NewMetadataAndImportKeysAreMappedWithoutShadowProperties()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var lab = db.Model.FindEntityType(typeof(LabTest))!;
        Assert.Contains(lab.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CompanyId", "ImportBatchId", "ImportRowNumber"]));
        Assert.DoesNotContain(lab.GetProperties(), x => x.IsShadowProperty());
        Assert.Equal("observedtext", db.Model.FindEntityType(typeof(LabTestResult))!.FindProperty("ObservedText")!.GetColumnName());
    }
}
