using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialPricingReadPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task LatestApprovedPriceIsSelectedBeforeMaterializationWithinCompanyCurrencyAndProduct()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var entity = db.Model.FindEntityType(typeof(ProductPricingVersion))!;
        await CreateTable(connection, entity);
        var company = Guid.NewGuid();
        var product = Guid.NewGuid();
        var emptyProduct = Guid.NewGuid();
        async Task Add(Guid tenant, Guid item, string currency, int version, bool active, ProductPricingStatus status, decimal price)
            => await Insert(connection, entity, new()
            {
                [nameof(ProductPricingVersion.ProductPricingVersionId)] = Guid.NewGuid(),
                [nameof(ProductPricingVersion.CompanyId)] = tenant, [nameof(ProductPricingVersion.ProductId)] = item,
                [nameof(ProductPricingVersion.Currency)] = currency, [nameof(ProductPricingVersion.Version)] = version,
                [nameof(ProductPricingVersion.IsActive)] = active, [nameof(ProductPricingVersion.Status)] = (int)status,
                [nameof(ProductPricingVersion.StandardSellingPrice)] = price, [nameof(ProductPricingVersion.PublisherNote)] = $"v{version}"
            });
        await Add(company, product, "VND", 1, true, ProductPricingStatus.Approved, 10);
        await Add(company, product, "VND", 2, true, ProductPricingStatus.Approved, 20);
        await Add(company, product, "VND", 3, false, ProductPricingStatus.Approved, 30);
        await Add(company, product, "VND", 4, true, ProductPricingStatus.Approved, 0);
        await Add(company, product, "VND", 5, true, ProductPricingStatus.Draft, 50);
        await Add(company, product, "USD", 99, true, ProductPricingStatus.Approved, 99);
        await Add(Guid.NewGuid(), product, "VND", 100, true, ProductPricingStatus.Approved, 100);
        await Add(company, Guid.NewGuid(), "VND", 100, true, ProductPricingStatus.Approved, 100);
        var query = GetSampleRequestSampleTrialsQueryHandler.LatestApprovedPrices(db.ProductPricingVersions.AsNoTracking(),
            company, [product, emptyProduct], "VND");
        var result = await query.Select(x => new { x.ProductId, x.Version, x.StandardSellingPrice, x.PublisherNote }).ToArrayAsync();
        var latest = Assert.Single(result);
        Assert.Equal(product, latest.ProductId);
        Assert.Equal(2, latest.Version);
        Assert.Equal(20, latest.StandardSellingPrice);
        Assert.Equal("v2", latest.PublisherNote);
        Assert.Contains("NOT EXISTS", query.Select(x => x.ProductPricingVersionId).ToQueryString());
    }
}
