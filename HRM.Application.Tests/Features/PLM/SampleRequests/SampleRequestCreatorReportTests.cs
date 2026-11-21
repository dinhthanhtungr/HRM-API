using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestCreatorReportTests
{
    [Fact]
    public void CountsOneSamplePerCreatorDespiteMultipleFormulasAndExcludesOtherCompanyAndInactive()
    {
        var company = Guid.NewGuid(); var product = Guid.NewGuid(); var sample = Guid.NewGuid();
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var excluded = Guid.NewGuid();
        var records = new[] { new SampleRequestAdminReportRecord { SampleRequestId = sample, ProductId = product } }.AsQueryable();
        var formulas = new[]
        {
            new Formula { ProductId = product, CompanyId = company, CreatedBy = first },
            new Formula { ProductId = product, CompanyId = company, CreatedBy = first },
            new Formula { ProductId = product, CompanyId = company, CreatedBy = second },
            new Formula { ProductId = product, CompanyId = Guid.NewGuid(), CreatedBy = excluded },
            new Formula { ProductId = product, CompanyId = company, CreatedBy = excluded, IsActive = false },
            new Formula { ProductId = Guid.NewGuid(), CompanyId = company, CreatedBy = excluded }
        }.AsQueryable();
        var result = SampleRequestCreatorReport.Contributions(records, formulas, company, [first, second, excluded]).ToArray();
        Assert.Equal(2, result.Length);
        Assert.All(result, row => Assert.Equal(sample, row.SampleRequestId));
        Assert.DoesNotContain(result, row => row.EmployeeId == excluded);
    }

    [Theory]
    [InlineData(SampleRequestAdminMetric.Completed)]
    [InlineData(SampleRequestAdminMetric.InProgress)]
    public void CreatorAggregationTranslatesOnPostgresWithoutLoadingSampleList(SampleRequestAdminMetric metric)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var records = SampleRequestAdminReportQuery.Build(db.SampleRequests.Where(s => s.CompanyId == company && s.IsActive), db, db.ProductPricingVersions);
        records = SampleRequestAdminReportQuery.Filter(records, metric, new(2026, 11, 1), new(2026, 12, 1));
        var sql = SampleRequestCreatorReport.Contributions(records, db.Formulas, company, [Guid.NewGuid()])
            .GroupBy(x => x.EmployeeId).Select(g => new { Employee = g.Key, Count = g.Count() }).ToQueryString();
        Assert.Contains("DISTINCT", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("CompanyId", sql);
    }
}
