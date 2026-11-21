using HRM.Application.Commons.Searching;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Commons.Searching;

public sealed class SmartSearchTests
{
    [Fact]
    public void CanonicalFieldKeyIsAcceptedOnlyForFieldsDeclaredByTheFeature()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = db.Customers.Where(x => x.CompanyId == Guid.Empty);
        var schema = SmartSearch.For(rows, f => [f.Code("customerCode", x => x.ExternalId, "Mã KH")]);
        var sql = schema.Apply(rows, "customerCode: KH_12", null, out var legacy).ToQueryString();
        Assert.Null(legacy); Assert.Contains("KH", sql); Assert.Contains("ILIKE", sql);
        schema.Apply(rows, "employeeName: Kha", null, out legacy);
        Assert.Equal("employeeName: Kha", legacy);
        var summary = HRM.Application.Features.PLM.SampleRequests.Services.Search.SampleRequestSearchInput.Parse("customerCode: KH_12");
        Assert.Equal("customerCode", summary.Type); Assert.Equal("KH_12", summary.Keyword);
    }
    [Fact]
    public void MyTaskSuggestionsTranslatePersonalAndCustomerSourcesWithActorScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var support = new HRM.Application.Features.Work.MyTasks.MyTaskSupport(db, db, null!, null!);
        var actor = new HRM.Application.Features.Work.MyTasks.MyTaskActor(Guid.NewGuid(), Guid.NewGuid());
        var rows = support.ProjectTasks(support.BuildPersonalTaskQuery(actor), new(2026, 1, 1))
            .Concat(support.ProjectTasks(support.BuildCustomerFollowUpTaskQuery(actor), new(2026, 1, 1)));
        var schema = HRM.Application.Features.Work.MyTasks.MyTaskSmartSearch.Build(rows, db.Customers.Where(c => c.CompanyId == actor.CompanyId));
        var sql = schema.BuildSuggestionQuery(rows, new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "Nam" })!.ToQueryString();
        Assert.Contains("CompanyId", sql); Assert.Contains("AssignedToEmployeeId", sql); Assert.Contains("UNION ALL", sql);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT @").Count);
    }

    [Fact]
    public void SalePricingFormulaSuggestionsUseOnlyApprovedVersionMetadata()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var rows = db.Products.Where(x => x.CompanyId == company && x.IsActive);
        var requests = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var versions = db.ProductPricingVersions.Where(x => x.CompanyId == company && x.IsActive &&
            x.Status == HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Approved && x.Currency == "VND");
        var schema = HRM.Application.Features.CRM.Quotations.Services.Queries.ProductPricingSmartSearch.Build(rows, requests, company, versions);
        var sql = schema.BuildSuggestionQuery(rows, new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "Công thức: VU" })!.ToQueryString();
        Assert.Contains("ProductPricingVersions", sql); Assert.Contains("FormulaExternalIdSnapshot", sql);
        Assert.Contains("Status", sql); Assert.Contains("VND", sql);
        Assert.DoesNotContain("MaterialCostSnapshot", sql); Assert.DoesNotContain("StandardSellingPrice", sql);
    }
    [Fact]
    public void AvailableStockSuggestionsApplyQuantityAtSqlPerFieldWithoutFetchingDetails()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = db.WarehouseShelfStocks.Where(x => x.CompanyId == Guid.Empty);
        var schema = HRM.Application.Features.Warehouse.Queries.GetStockAvailable.StockAvailableSmartSearch.Build(db, rows, Guid.Empty)
            .FilterSuggestionScope(q => HRM.Application.Features.Warehouse.Queries.GetStockAvailable.StockAvailableSmartSearch.FilterAvailability(db, q, Guid.Empty, true, 10m));
        var sql = schema.BuildSuggestionQuery(rows, new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "NVL" })!.ToQueryString();
        Assert.Contains("GROUP BY", sql); Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyId", sql); Assert.Contains("stockType", sql, StringComparison.OrdinalIgnoreCase); Assert.Contains("Open", sql);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT").Count);
    }

    [Fact]
    public void PlpuSuggestionsTranslateBothSourcesAndKeepOperationalRestrictions()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var repository = new HRM.Application.Features.Reports.PLPU.PLPUPurchaseOverviewReportRepository(db);
        var request = new HRM.Application.Features.Reports.PLPU.PLPUPurchaseOverviewQuery { CompanyId = Guid.NewGuid(), SuggestionKeyword = "Tên NVL: hạt" };
        var rows = repository.BuildSearchRows(request);
        var schema = HRM.Application.Features.Reports.PLPU.PLPUSmartSearch.Build(rows, x => x.Order, x => x.SupplierCode,
            x => x.SupplierName, x => x.MaterialCode, x => x.MaterialName);
        var sql = schema.BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.Contains("UNION ALL", sql); Assert.Contains("CompanyId", sql); Assert.Contains("NVL%", sql);
        Assert.Contains("%VPP%", sql); Assert.Contains("%BB%", sql); Assert.Contains("~*", sql);
    }
    [Fact]
    public void MovementSuggestionsMatchCompleteGroupsAndKeepDateScopeInSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var visible = db.WarehouseMovementRows.Where(x => x.CompanyId == Guid.Empty);
        var groups = visible.GroupBy(x => x.MovementKey).Where(g => g.Min(x => x.MovementAt) >= new DateTime(2026, 1, 1));
        var schema = HRM.Application.Features.Warehouse.Queries.GetStockMovements.StockMovementSmartSearch.Build(visible);
        var request = new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "NVL",
            SearchSelections = "[{\"field\":\"batchNumber\",\"value\":\"BATCH_1\"}]" };
        var sql = schema.BuildSuggestionQuery(groups, request)!.ToQueryString();
        Assert.Contains("GROUP BY", sql); Assert.Contains("HAVING", sql);
        Assert.Contains("CompanyId", sql); Assert.Contains("BATCH_1", sql);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT").Count);
    }

    [Fact]
    public void BomSuggestionsKeepTypeAndTenantAndTranslateSourceFormulaBranches()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = HRM.Application.Features.PLM.Boms.Queries.GetBoms.BomSearchQueryRules.Build(
            db, Guid.Empty, null, HRM.Domain.Enums.Boms.BomType.Engineering, null,
            new HRM.Application.Commons.Pagination.PaginationQuery(), out var schema);
        var sql = schema.BuildSuggestionQuery(rows, new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "VU" })!.ToQueryString();
        Assert.Contains("CompanyId", sql); Assert.Contains("IsActive", sql);
        Assert.Contains("Formulas", sql); Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT").Count);
    }

    [Fact]
    public void MaterialEquivalenceSuggestionsOnlyUseActualSelectedFormulaCandidates()
    {
        var material = new HRM.Application.Features.DevAndQA.MaterialEquivalences.EquivalentMaterialDto(Guid.NewGuid(), "NVL_1", "Nam Thành", Guid.NewGuid(), "kg");
        var row = new HRM.Application.Features.DevAndQA.MaterialEquivalences.MaterialEquivalenceDto(Guid.NewGuid(), "TP_1", "BB_1", "Loãng màu", "L",
            Guid.NewGuid(), "VU_1", "Matched", [material], [], []);
        var request = new HRM.Application.Features.DevAndQA.MaterialEquivalences.GetMaterialEquivalencesQuery { SuggestionKeyword = "Tên NVL: thành nam" };
        var result = HRM.Application.Features.DevAndQA.MaterialEquivalences.MaterialEquivalenceSearch.Suggest([row], request);
        Assert.Equal("Nam Thành", Assert.Single(result).Value);
    }
    private sealed record Row(string Code, string Name, string[] Products);
    private static SmartSearchSchema<Row> Schema(IQueryable<Row> query) => SmartSearch.For(query, fields => [
        fields.Code("code", x => x.Code, "Mã KH"),
        fields.Name("name", x => x.Name, "Tên KH"),
        fields.Names("product", x => x.Products, x => x, "Tên sản phẩm")
    ]);

    [Fact]
    public void SameFieldUsesOrAndDifferentFieldsUseAndWithinTheOriginalScope()
    {
        var query = new[] { new Row("A", "Nam Thành", ["Hạt phụ gia"]),
            new Row("B", "Thành Nam", ["Bột màu"]), new Row("C", "Khác", ["Hạt phụ gia"]) }.AsQueryable();
        var filtered = Schema(query).Apply(query, null,
            "[{\"field\":\"code\",\"value\":\"A\"},{\"field\":\"code\",\"value\":\"B\"},{\"field\":\"product\",\"value\":\"Hạt phụ gia\"}]", out _);
        Assert.Equal("A", Assert.Single(filtered).Code);
        Assert.Empty(Schema(query).Apply(query.Where(x => x.Code == "B"), null,
            "[{\"field\":\"code\",\"value\":\"A\"}]", out _));
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("[{\"field\":\"secret\",\"value\":\"A\"}]")]
    [InlineData("[null]")]
    public void InvalidSelectionsFailClosed(string selection)
    {
        var query = new[] { new Row("A", "Nam Thành", []) }.AsQueryable();
        Assert.Empty(Schema(query).Apply(query, null, selection, out _));
    }

    [Fact]
    public void KnownLabelOnlySearchesItsFieldAndNamesIgnoreWordOrder()
    {
        var query = new[] { new Row("ABC", "Nam Thành", []), new Row("NAM", "Khác", []) }.AsQueryable();
        Assert.Equal("ABC", Assert.Single(Schema(query).Apply(query, " TÊN KH： thành nam ", null, out var legacy)).Code);
        Assert.Null(legacy);
        Schema(query).Apply(query, "Unknown: nam", null, out legacy);
        Assert.Equal("Unknown: nam", legacy);
        Assert.Empty(Schema(query).Apply(query, "Tên KH:", null, out _));
    }

    [Fact]
    public void ScalarAndCollectionPredicatesTranslateWithoutClientEvaluation()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = db.Customers.Where(x => x.CompanyId == Guid.Empty);
        var schema = SmartSearch.For(query, fields => [fields.Name("name", x => x.CustomerName, "Tên KH")]);
        var sql = schema.Apply(query, "Tên KH: thành nam", null, out _).ToQueryString();
        Assert.Contains("~*", sql); Assert.Contains("CompanyId", sql);
        var products = db.Products.Where(x => x.CompanyId == Guid.Empty);
        var many = SmartSearch.For(products, fields => [fields.Codes("tp", x => x.SampleRequests.Where(s => s.IsActive),
            s => s.ExternalId, "TP Code")]);
        sql = many.Apply(products, "TP Code: TP_1", null, out _).ToQueryString();
        Assert.Contains("EXISTS", sql); Assert.Contains("ILIKE", sql);
        sql = many.Apply(products, null, "[{\"field\":\"tp\",\"value\":\"TP_1\"}]", out _).ToQueryString();
        Assert.Contains("EXISTS", sql); Assert.Contains("TP_1", sql);
    }

    [Fact]
    public void SelectionFillsUnusedSlotsAndNeverExceedsTen()
    {
        var candidates = Enumerable.Range(0, 20).Select(x => new SmartSearchSuggestion { Field = "a", Value = x.ToString() })
            .Append(new SmartSearchSuggestion { Field = "b", Value = "only" });
        var result = SmartSearchSchema<Row>.Select(candidates);
        Assert.Equal(10, result.Count); Assert.Single(result, x => x.Field == "b");
    }

    [Fact]
    public void SuggestionSqlKeepsTenantAndOtherFieldsButAllowsAnotherSelectionInTheSameField()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = db.Customers.Where(x => x.CompanyId == Guid.Empty);
        var schema = SmartSearch.For(rows, fields => [fields.Code("code", x => x.ExternalId, "Mã KH"),
            fields.Name("name", x => x.CustomerName, "Tên KH")]);
        var request = new HRM.Application.Commons.Pagination.PaginationQuery {
            SuggestionKeyword = "Tên KH: nam", SearchSelections =
                "[{\"field\":\"name\",\"value\":\"OLD_NAME\"},{\"field\":\"code\",\"value\":\"KH_42\"}]"
        };
        var scoped = schema.ApplyRequest(rows, null, request, out _);
        var sql = schema.BuildSuggestionQuery(scoped, request)!.ToQueryString();
        Assert.Contains("CompanyId", sql); Assert.Contains("KH_42", sql); Assert.DoesNotContain("OLD_NAME", sql);
        Assert.DoesNotContain("UNION ALL", sql); Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void RelatedQueryAndNavigationSuggestionsTranslateWithABoundedLimitPerField()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = db.Products.Where(x => x.CompanyId == Guid.Empty);
        var schema = SmartSearch.For(rows, fields => [
            fields.Codes("formula", x => db.Formulas.Where(f => f.CompanyId == Guid.Empty && f.ProductId == x.ProductId && f.IsActive),
                f => f.ExternalId, "Công thức"),
            fields.Codes("tp", x => x.SampleRequests.Where(s => s.CompanyId == Guid.Empty && s.IsActive), s => s.ExternalId, "TP Code")
        ]);
        var sql = schema.BuildSuggestionQuery(rows, new HRM.Application.Commons.Pagination.PaginationQuery { SuggestionKeyword = "VU" })!.ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT").Count);
        Assert.Contains("IsActive", sql); Assert.Contains("CompanyId", sql);
    }

    [Fact]
    public void IqcCombinedSearchTranslatesBeforePagingWithoutDroppingCompanyScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new HRM.Application.Features.DevAndQA.IQC.Services.IqcReader(db, Guid.Empty);
        var request = new HRM.Application.Features.DevAndQA.IQC.Dtos.QCInputQuery { IncludeLabTests = true, SuggestionKeyword = "Hạt" };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.Contains("UNION ALL", sql); Assert.Contains("CompanyId", sql);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT @").Count);
    }
}
