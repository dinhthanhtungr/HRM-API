using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Application.Features.Purchasing.Materials.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

internal sealed class MaterialReviewClock : IDateTimeProvider { public DateTime Now => new(2026, 10, 22); }

public sealed class PurchasingMaterialReviewTests
{
    [Theory]
    [InlineData(MaterialPriceReviewStatus.MissingPrice)]
    [InlineData(MaterialPriceReviewStatus.NeedsReview)]
    [InlineData(MaterialPriceReviewStatus.UpToDate)]
    public async Task FiltersBeforeCountAndPaging_WithBoundaryZeroAndMissingDate(MaterialPriceReviewStatus status)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var company = user.CompanyId!.Value;
        var cutoff = new MaterialReviewClock().Now.AddDays(-30);
        Material Material(string code, decimal? amount, DateTime? date) => new()
        {
            MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true, ExternalId = code,
            Category = new() { CompanyId = company },
            MaterialsSuppliers = [new() { MaterialsSuppliersId = Guid.NewGuid(), IsActive = true, CurrentPrice = amount,
                UpdatedDate = date, Supplier = new() { CompanyId = company, IsActive = true } }]
        };
        store.Seed(nameof(db.Materials), Material("A", null, null), Material("B", 10, null),
            Material("C", 10, cutoff.AddTicks(-1)), Material("D", 0, cutoff), Material("E", 10, cutoff.AddDays(1)));
        var handler = new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock());
        var page = await handler.Handle(new() { PriceStatus = status, PageSize = 1, SortDirection = "asc" }, default);
        Assert.Equal(status == MaterialPriceReviewStatus.MissingPrice ? 1 : 2, page.TotalCount);
        Assert.Equal(status, Assert.Single(page.Items).ReviewStatus);
        Assert.Equal(status != MaterialPriceReviewStatus.MissingPrice, page.HasNextPage);
    }

    [Fact]
    public async Task PriceFilteringRequiresPriceCapability_AndValidEnum()
    {
        var (db, _) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] };
        var handler = new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new() { PriceStatus = MaterialPriceReviewStatus.MissingPrice }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new() { PriceStatus = (MaterialPriceReviewStatus)999 }, default));
        Assert.Equal(1, new GetPurchasingMaterialsQuery { StaleAfterDays = -1 }.NormalizedStaleAfterDays);
        Assert.Equal(365, new GetPurchasingMaterialsQuery { StaleAfterDays = 999 }.NormalizedStaleAfterDays);
    }

    [Theory]
    [InlineData("createdDate", "asc")]
    [InlineData("createdDate", "desc")]
    [InlineData("name", "asc")]
    [InlineData("externalId", "desc")]
    public async Task EveryFilteredPageMatchesUnfilteredPriceClassification(string sortBy, string direction)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var company = user.CompanyId!.Value;
        var cutoff = new MaterialReviewClock().Now.AddDays(-30);
        var materials = new List<Material>();
        // Cross-product covers absent sources, null/negative/zero prices, missing dates,
        // cutoff boundary, both-source selection, and duplicate master sort keys.
        (decimal? Price, DateTime? Date)[] values = [(null, null), (-1, cutoff.AddDays(1)),
            (0, cutoff), (10, null), (20, cutoff.AddTicks(-1)), (30, cutoff.AddDays(1))];
        for (var p = -1; p < values.Length; p++)
        for (var s = -1; s < values.Length; s++)
        {
            var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true,
                Name = "Same", ExternalId = "Same", CreatedDate = cutoff, Category = new() { CompanyId = company } };
            if (p >= 0)
                material.PurchaseOrderDetails = [new() { PurchaseOrderDetailId = Guid.NewGuid(), MaterialId = material.MaterialId,
                    IsActive = true, UnitPriceAgreed = values[p].Price, PurchaseOrder = new() { CompanyId = company,
                        IsActive = true, CreateDate = values[p].Date, Supplier = new() { CompanyId = company } } }];
            if (s >= 0)
                material.MaterialsSuppliers = [new() { MaterialsSuppliersId = Guid.NewGuid(), IsActive = true,
                    CurrentPrice = values[s].Price, UpdatedDate = values[s].Date,
                    Supplier = new() { CompanyId = company, IsActive = true } }];
            materials.Add(material);
        }
        // Latest invalid candidates must mask older valid rows, while foreign/canceled/inactive rows do not participate.
        materials[0].MaterialsSuppliers = [new() { MaterialsSuppliersId = Guid.NewGuid(), IsActive = true,
            CurrentPrice = 1, UpdatedDate = cutoff, Supplier = new() { CompanyId = company, IsActive = true } },
            new() { MaterialsSuppliersId = Guid.NewGuid(), IsActive = true, CurrentPrice = null,
                UpdatedDate = cutoff.AddDays(1), Supplier = new() { CompanyId = company, IsActive = true } }];
        materials[0].PurchaseOrderDetails = [new() { PurchaseOrderDetailId = Guid.NewGuid(), IsActive = true,
            UnitPriceAgreed = 100, PurchaseOrder = new() { CompanyId = company, IsActive = true,
                Status = "Canceled", CreateDate = cutoff.AddDays(2), Supplier = new() { CompanyId = company } } }];
        materials.Add(new() { MaterialId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true });
        materials.Add(new() { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = false });
        store.Seed(nameof(db.Materials), materials.ToArray());
        var handler = new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock());
        var all = await handler.Handle(new() { PageSize = 100, SortBy = sortBy, SortDirection = direction }, default);
        Assert.Equal(49, all.TotalCount);
        foreach (var status in Enum.GetValues<MaterialPriceReviewStatus>())
        {
            var expected = all.Items.Where(x => x.ReviewStatus == status).Select(x => x.MaterialId).ToArray();
            var actual = new List<Guid>();
            for (var page = 1; ; page++)
            {
                var result = await handler.Handle(new() { PriceStatus = status, PageSize = 3, PageNumber = page,
                    SortBy = sortBy, SortDirection = direction }, default);
                Assert.Equal(expected.Length, result.TotalCount);
                Assert.All(result.Items, x => Assert.Equal(status, x.ReviewStatus));
                actual.AddRange(result.Items.Select(x => x.MaterialId));
                if (!result.HasNextPage) break;
            }
            Assert.Equal(expected, actual);
            Assert.Equal(actual.Count, actual.Distinct().Count());
        }
    }

    [Theory]
    [InlineData(MaterialPriceReviewStatus.MissingPrice)]
    [InlineData(MaterialPriceReviewStatus.NeedsReview)]
    [InlineData(MaterialPriceReviewStatus.UpToDate)]
    public void PriceReviewStatusTranslatesToPostgres(MaterialPriceReviewStatus status)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var materials = db.Materials.Where(x => x.CompanyId == company && x.IsActive == true);
        var filtered = PurchasingMaterialPriceReview.Filter(materials, company,
            new MaterialReviewClock().Now.AddDays(-30), status);
        var sql = filtered.OrderBy(x => x.MaterialId).Take(15).ToQueryString();
        Assert.Contains("LEFT JOIN", sql);
        Assert.DoesNotContain("EXISTS", sql);
        Assert.DoesNotContain(" IN (SELECT", sql);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN LATERAL").Count);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("CompanyId", sql);
    }
}
