using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcSqlTranslationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void ListAndReportSelectors_TranslateWithAndWithoutQcFilter(bool? hasQc)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var filter = new QCInputQuery { HasQC = hasQc, Keyword = "MAT", FromDate = new(2026, 10, 1), ToDate = new(2026, 10, 31), VoucherDetailType = VoucherDetailType.Waiter };
        var idsSql = reader.BuildSummaryIdsQuery(filter).Skip(10).Take(15).ToQueryString();
        Assert.Contains("ILIKE", idsSql);
        Assert.Contains("companyid", idsSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", idsSql);
        Assert.Contains("WarehouseShelfLedger", idsSql);
        Assert.Contains("max(", idsSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("createdAt", idsSql);
        Assert.Contains("LEFT JOIN", idsSql);

        Assert.Contains("companyid", reader.BuildSummaryItemsQuery(new long[] { 1, 2 }).ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("companyid", reader.BuildExportSummaryItemsQuery(new long[] { 1, 2 }).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetailWithLatestQcAndSupplier_TranslatesToScopedPostgresSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildDetailQuery(1).ToQueryString();
        Assert.Contains("LIMIT 1", sql);
        Assert.Contains("companyid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("createddate", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExportFiltersAndNullQc_TranslateToPostgres(bool hasQc)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildExportRowsQuery(new QCInputQuery
        {
            HasQC = hasQc, Keyword = "MAT", FromDate = new(2026, 1, 1), ToDate = new(2026, 12, 31),
            VoucherDetailType = VoucherDetailType.Special
        }).ToQueryString();
        Assert.Contains("ILIKE", sql);
        Assert.Contains("companyid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASE", sql);
        Assert.Contains("WarehouseShelfLedger", sql);
        Assert.Contains("max(", sql, StringComparison.OrdinalIgnoreCase);
    }
}
