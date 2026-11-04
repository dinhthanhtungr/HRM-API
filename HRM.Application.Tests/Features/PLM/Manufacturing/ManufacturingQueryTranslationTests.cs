using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingQueryTranslationTests
{
    [Fact]
    public void FormulaChoices_TranslateCorrelatedLatestSelectionAndStandardForPostgreSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices.GetManufacturingFormulaChoicesQueryHandler
            .Build((IManufacturingDbContext)db, Guid.NewGuid(), new DateTime(2026, 10, 10), new() { Keyword = "BLUE", OrderId = Guid.NewGuid() });
        var sql = query.OrderByDescending(x => x.IsSelected).ThenByDescending(x => x.IsStandard).ThenBy(x => x.CreatedDate).Take(15).ToQueryString();
        Assert.Contains("ILIKE", sql);
        Assert.Contains("company_id", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormulaHeaders_TranslateUnionKeywordScopeAndPaginationForPostgreSql(bool compare)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = ManufacturingFormulaHeaderQuery.Build((IManufacturingDbContext)db, Guid.NewGuid(), new() {
            Keyword = "VA", ProductId = Guid.NewGuid(), From = new(2026, 1, 1), To = new(2026, 12, 31) }, compare);
        var sql = query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id).Skip(5).Take(10).ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("ILIKE", sql);
        Assert.Contains("company_id", sql);
        Assert.Contains("LIMIT", sql);
        if (compare) Assert.Contains("NOT EXISTS", sql);
    }

    [Fact]
    public void ColorChip_LegacyColumnsAreMappedWithoutRequiringSchemaCreation()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.Model.FindEntityType(typeof(ColorChipManufacturingRecord))!;
        Assert.Equal("color_code", model.FindProperty(nameof(ColorChipManufacturingRecord.ColorCode))!.GetColumnName());
        Assert.Equal("product_name", model.FindProperty(nameof(ColorChipManufacturingRecord.ProductName))!.GetColumnName());
        Assert.Equal("usage_rate", model.FindProperty(nameof(ColorChipManufacturingRecord.UsageRate))!.GetColumnName());
    }
}
