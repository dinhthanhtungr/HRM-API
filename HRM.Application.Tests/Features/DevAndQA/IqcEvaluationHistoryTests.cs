using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class IqcEvaluationHistoryTests
{
    [Fact]
    public async Task HistoryPagesWholeEvaluationsAndExcludesCurrentOtherMaterialCompanyAndFuture()
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User();
        var company = user.CompanyId!.Value;
        var now = new DateTime(2026, 10, 8);
        var standard = new EvaluationStandard { Id = Guid.NewGuid(), CompanyId = company, Code = "MOISTURE", Name = "Original" };
        IqcInspectionEvaluation Row(long id, DateTime date, string code = "A", Guid? owner = null)
        {
            var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = id, CreatedDate = date,
                WarehouseVoucherDetail = new WarehouseVoucherDetail { VoucherDetailId = id, ProductCode = code, QtyKg = id * 10.25m,
                    LotNumber = $"LOT-{id}", Voucher = new WarehouseVoucher { CompanyId = owner ?? company } } };
            var evaluation = new IqcInspectionEvaluation { Id = Guid.NewGuid(), QCInputByQCId = qc.QCInputByQCId, QCInputByQC = qc };
            var result = EvaluationSnapshotFactory.CreateAdditionalIqc(standard, evaluation.Id, company);
            result.Value = id.ToString();
            evaluation.Results.Add(result);
            return evaluation;
        }
        var current = Row(10, now);
        var histories = new[] { Row(1, now.AddDays(-3)), Row(2, now.AddDays(-2)), Row(3, now.AddDays(-1)),
            current, Row(4, now.AddDays(-1), "B"), Row(5, now.AddDays(-1), owner: Guid.NewGuid()), Row(6, now.AddDays(1)) };
        store.Seed("WarehouseVoucherDetails", current.QCInputByQC.WarehouseVoucherDetail);
        store.Seed("QCInputByQCs", current.QCInputByQC);
        store.Seed("IqcInspectionEvaluations", histories);
        standard.Name = "Changed catalog";
        var service = new IqcEvaluationHistoryReader(db, user, new CurrentUserPermissionService(user));

        var first = await service.GetIqcEvaluationHistoryAsync(new(10, PageSize: 2), default);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(new long[] { 3, 2 }, first.Items.Select(x => x.VoucherDetailId));
        Assert.True(first.HasNextPage);
        Assert.All(first.Items, x =>
        {
            Assert.Equal($"LOT-{x.VoucherDetailId}", x.LotNumber);
            Assert.Equal(x.VoucherDetailId * 10.25m, x.QtyKg);
            Assert.Equal("kg", x.Unit);
            Assert.True(x.Evaluation.IsStored);
            Assert.Equal("Original", Assert.Single(x.Evaluation.Results).CriterionName);
            Assert.Equal(x.VoucherDetailId.ToString(), x.Evaluation.Results[0].Value);
        });
        var second = await service.GetIqcEvaluationHistoryAsync(new(10, 2, 2), default);
        Assert.Equal(1, Assert.Single(second.Items).VoucherDetailId);
        Assert.False(second.HasNextPage);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ForeignParentReturns404AndInvalidPaginationReturns400()
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User();
        store.Seed("WarehouseVoucherDetails", new WarehouseVoucherDetail { VoucherDetailId = 1,
            Voucher = new WarehouseVoucher { CompanyId = Guid.NewGuid() } });
        var service = new IqcEvaluationHistoryReader(db, user, new CurrentUserPermissionService(user));
        Assert.Equal(404, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.GetIqcEvaluationHistoryAsync(new(1), default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.GetIqcEvaluationHistoryAsync(new(1, 0), default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<EvaluationRequestException>(() =>
            service.GetIqcEvaluationHistoryAsync(new(1, PageSize: 51), default))).StatusCode);
    }

    [Fact]
    public async Task NewQcWithoutHistoryReturnsEmptyPagedResult()
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User();
        store.Seed("WarehouseVoucherDetails", new WarehouseVoucherDetail { VoucherDetailId = 1, ProductCode = "A",
            Voucher = new WarehouseVoucher { CompanyId = user.CompanyId!.Value } });
        var service = new IqcEvaluationHistoryReader(db, user, new CurrentUserPermissionService(user));
        var result = await service.GetIqcEvaluationHistoryAsync(new(1), default);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public void HistoryQueryTranslatesCompanyMaterialParentDateAndResultsToPostgresql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new IqcWorkflowTests.User();
        var service = new IqcEvaluationHistoryReader(db, user, new CurrentUserPermissionService(user));
        var sql = service.BuildIqcHistoryQuery(user.CompanyId!.Value, 123, "A", new DateTime(2026, 10, 8))
            .OrderByDescending(x => x.QCInputByQC.CreatedDate).ThenByDescending(x => x.QCInputByQCId)
            .Skip(3).Take(3).ToQueryString();
        Assert.Contains("EXISTS", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("company", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("productcode", sql, StringComparison.OrdinalIgnoreCase);
    }
}
