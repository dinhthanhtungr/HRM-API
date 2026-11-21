using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Commons.Searching;

public sealed class CustomerTaxSearchTests
{
    [Theory]
    [InlineData("MST")]
    [InlineData("MST KH")]
    [InlineData("Mã số thuế")]
    [InlineData("MA SO THUE KH")]
    [InlineData("Mã số thuế khách hàng")]
    [InlineData("Tax code")]
    [InlineData("Customer tax number")]
    [InlineData("customerTaxNumber")]
    public void TaxAliasesUseLiteralCodeSearchAndSummaryRecognizesTheSameField(string label)
    {
        using var db = Database();
        var company = Guid.NewGuid();
        var rows = db.Customers.Where(c => c.CompanyId == company);
        var schema = SmartSearch.For(rows, fields => [fields.CustomerTaxNumber(c => c.TaxNumber)]);
        var sql = schema.BuildSuggestionQuery(rows, new PaginationQuery { SuggestionKeyword = label + ": 001234-001" })!.ToQueryString();
        Assert.Contains("TaxNumber", sql);
        Assert.Contains("ILIKE", sql);
        Assert.Contains("001234-001", sql);
        Assert.Contains("CompanyId", sql);
        var input = SampleRequestSearchInput.Parse(label + ": 001234-001");
        Assert.Equal("customerTaxNumber", input.Type);
        Assert.Equal("001234-001", input.Keyword);
    }

    [Fact]
    public void ExactTaxBadgesKeepVisibilityAndOrWithinFieldAndAcrossFields()
    {
        var company = Guid.NewGuid();
        var visibleId = Guid.NewGuid();
        var customers = new[] {
            new Customer { CustomerId = visibleId, CompanyId = company, ExternalId = "A", TaxNumber = "001-001" },
            new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, ExternalId = "B", TaxNumber = "002" },
            new Customer { CustomerId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ExternalId = "A", TaxNumber = "001-001" },
            new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, ExternalId = "C", TaxNumber = null }
        }.AsQueryable();
        var visible = customers.Where(c => c.CompanyId == company);
        var schema = SmartSearch.For(visible, fields => [fields.CustomerTaxNumber(c => c.TaxNumber), fields.Code("customerCode", c => c.ExternalId)]);
        const string selections = "[{\"field\":\"customerTaxNumber\",\"value\":\"001-001\"},{\"field\":\"customerTaxNumber\",\"value\":\"002\"}]";
        Assert.Equal(2, schema.Apply(visible, null, selections, out _).Count());
        var combined = selections[..^1] + ",{\"field\":\"customerCode\",\"value\":\"A\"}]";
        Assert.Equal(visibleId, Assert.Single(schema.Apply(visible, null, combined, out _)).CustomerId);
    }

    [Fact]
    public void SaleOrderAndPricingTaxSuggestionsTranslateInsideTheExistingCompanyAndVisibleRequestQueries()
    {
        using var db = Database();
        var company = Guid.NewGuid();
        var orders = db.MerchandiseOrders.Where(x => x.CompanyId == company);
        var sale = HRM.Application.Features.PLM.SaleOrders.Services.SaleOrderSmartSearch.Build(orders, company);
        var request = new PaginationQuery { SuggestionKeyword = "MST: 001" };
        var saleSql = sale.BuildSuggestionQuery(orders, request)!.ToQueryString();
        Assert.Contains("TaxNumber", saleSql);
        Assert.Contains("CompanyId", saleSql);
        var products = db.Products.Where(x => x.CompanyId == company);
        var visibleRequests = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var pricing = HRM.Application.Features.CRM.Quotations.Services.Queries.ProductPricingSmartSearch.Build(products, visibleRequests, company);
        var pricingSql = pricing.BuildSuggestionQuery(products, request)!.ToQueryString();
        Assert.Contains("TaxNumber", pricingSql);
        Assert.Contains("CompanyId", pricingSql);
        Assert.Contains("IsActive", pricingSql);
    }

    [Fact]
    public void SummaryTaxSuggestionsFreeKeywordAndBadgesUseCurrentCustomerMetadata()
    {
        using var db = Database();
        var company = Guid.NewGuid();
        var visible = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var suggestions = SummarySuggestionQueries.Build(db, visible, "MST: 001").ToQueryString();
        Assert.Contains("TaxNumber", suggestions);
        Assert.Contains("CompanyId", suggestions);
        foreach (var keyword in new[] { "001234-001", "MST: 001234-001" })
            Assert.Contains("TaxNumber", SampleRequestSummaryKeywordSearch.Apply(visible, db, keyword).ToQueryString());
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var rows = new[] {
            new SampleRequest { CustomerId = customerA, CompanyId = company, Customer = new Customer { CompanyId = company } },
            new SampleRequest { CustomerId = customerB, CompanyId = company, Customer = new Customer { CompanyId = company } }
        }.AsQueryable();
        Assert.Empty(SampleRequestSummaryQueryRules.ApplySelections(rows, db, new SampleRequestSummaryFilters { CustomerTaxNumberIds = [customerA], CustomerIds = [customerB] }));
        Assert.Equal(customerA, Assert.Single(SampleRequestSummaryQueryRules.ApplySelections(rows, db,
            new SampleRequestSummaryFilters { CustomerTaxNumberIds = [customerA], CustomerIds = [customerA] })).CustomerId);
        var invalid = new SampleRequestSummaryFilters { CustomerTaxNumberIds = [Guid.Empty] };
        Assert.False(Validator.TryValidateObject(invalid, new(invalid), [], true));
        var tooMany = new SampleRequestSummaryFilters { CustomerTaxNumberIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray() };
        Assert.False(Validator.TryValidateObject(tooMany, new(tooMany), [], true));
    }

    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
}
