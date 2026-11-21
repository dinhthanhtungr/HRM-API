using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSummarySearchTests
{
    private static ApplicationDbContext Database() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);

    [Theory]
    [InlineData("tran thanh hien", "Trần Thanh Hiền")]
    [InlineData("Kha", "Nguyễn Văn Kha")]
    [InlineData("Đặng", "Dang")]
    [InlineData("BB%_", "BB%_123")]
    public void SuggestionMatchingSupportsAccentsAndLiteralInput(string input, string name)
    {
        var pattern = SampleRequestSearchText.Pattern(input);
        Assert.True(Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase));
        if (input == "BB%_") Assert.False(Regex.IsMatch("BBXYZ", pattern, RegexOptions.IgnoreCase));
    }

    [Fact]
    public void MultipleEmployeesUseUnionWhileColourCodesUseIntersection()
    {
        using var db = Database();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var other = Guid.NewGuid();
        SampleRequest Row(Guid employee, string code) => new()
            { ManagerBy = employee, Product = new Product { ColourCode = code } };
        var rows = new[] { Row(a, "BB1"), Row(b, "BB1"), Row(a, "XX"), Row(other, "BB1") }.AsQueryable();
        var result = SampleRequestSummaryQueryRules.ApplySelections(rows, db,
            new SampleRequestSummaryFilters { EmployeeIds = [a, b], ColourCodes = ["BB1"] }).ToArray();
        Assert.Equal(2, result.Length);
        Assert.All(result, row => Assert.Equal("BB1", row.Product.ColourCode));
    }

    [Fact]
    public void SuggestionAndSelectionQueriesTranslateBeforePagingWithoutJoinsMultiplyingRequests()
    {
        using var db = Database();
        var selected = SampleRequestSummaryQueryRules.ApplySelections(db.SampleRequests, db,
            new SampleRequestSummaryFilters
            {
                EmployeeIds = [Guid.NewGuid()], GroupIds = [Guid.NewGuid()],
                FormulaIds = [Guid.NewGuid()], ColourCodes = ["BB1"]
            });
        var sql = selected.OrderBy(x => x.SampleRequestId).Take(10).ToQueryString();
        Assert.Contains("EXISTS", sql); Assert.Contains("CompanyId", sql); Assert.Contains("LIMIT", sql);
        var candidates = db.Employees.Select(e => new SummarySuggestionQueries.Candidate
            { Type = "employee", Value = e.EmployeeId.ToString(), Label = e.FullName, Code = e.ExternalId }).Distinct();
        var suggestionSql = SummarySuggestionQueries.MatchText(
            candidates, "hien", SampleRequestSearchText.Pattern("hien")).ToQueryString();
        Assert.Contains("~*", suggestionSql); Assert.Contains("LIMIT", suggestionSql);
        Assert.DoesNotContain("ILIKE", suggestionSql);
        var combined = SummarySuggestionQueries.Build(db,
            db.SampleRequests.Where(x => x.IsActive && x.CompanyId == Guid.Empty), "hien").ToQueryString();
        Assert.Equal(9, Regex.Matches(combined, "UNION ALL").Count);
        Assert.Equal(10, Regex.Matches(combined, "LIMIT").Count);
        var codeSql = SummarySuggestionQueries.MatchCode(candidates, "BB%_").ToQueryString();
        Assert.Contains("ILIKE", codeSql);
        Assert.DoesNotContain("~*", codeSql);
    }

    [Fact]
    public void CustomerCodeNameAndProductNameAreIndependentFilterFields()
    {
        using var db = Database();
        var customerA = Guid.NewGuid(); var customerB = Guid.NewGuid();
        var product = Guid.NewGuid();
        var rows = new[] {
            new SampleRequest { CustomerId = customerA, ProductId = product },
            new SampleRequest { CustomerId = customerB, ProductId = product }
        }.AsQueryable();
        Assert.Empty(SampleRequestSummaryQueryRules.ApplySelections(rows, db,
            new SampleRequestSummaryFilters { CustomerCodeIds = [customerA], CustomerIds = [customerB] }));
        var filtered = SampleRequestSummaryQueryRules.ApplySelections(rows, db,
            new SampleRequestSummaryFilters { CustomerCodeIds = [customerA], CustomerIds = [customerA], ProductIds = [product] });
        Assert.Equal(customerA, Assert.Single(filtered).CustomerId);
    }

    [Fact]
    public void EndUserSupportsScopedFreeTextSuggestionsAndExactSelections()
    {
        using var db = Database();
        Assert.Equal("endUser", SampleRequestSearchInput.Parse("End user: Toyota").Type);
        var rows = new[] {
            new SampleRequest { Product = new Product { IsActive = true, EndUser = "Toyota" } },
            new SampleRequest { Product = new Product { IsActive = true, EndUser = "Ford" } },
            new SampleRequest { Product = new Product { IsActive = false, EndUser = "Toyota" } },
            new SampleRequest { Product = new Product { IsActive = true } }
        }.AsQueryable();
        Assert.Single(SampleRequestSummaryQueryRules.ApplySelections(rows, db,
            new SampleRequestSummaryFilters { EndUsers = ["Toyota"] }));
        foreach (var keyword in new[] { "Toyota", "End user: Toyota" })
            Assert.Contains("EndUser", SampleRequestSummaryKeywordSearch.Apply(db.SampleRequests, db, keyword).ToQueryString());
        var company = Guid.NewGuid();
        var sql = SummarySuggestionQueries.Build(db, db.SampleRequests.Where(x => x.CompanyId == company),
            "End user: Toyota").ToQueryString();
        Assert.Contains(company.ToString(), sql);
        Assert.Contains("EndUser", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("UNION ALL", sql);
        var invalid = new SampleRequestSummaryFilters { EndUsers = [" "] };
        Assert.NotEmpty(invalid.Validate(new ValidationContext(invalid)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordScopeRejectsOtherCompaniesAndRespectsCustomerOwnership(bool fullView)
    {
        var company = Guid.NewGuid(); var employee = Guid.NewGuid();
        var mine = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            CustomerAssignments = [new CustomerAssignment { EmployeeId = employee, IsActive = true }] };
        var unrelated = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var rows = new[]
        {
            new SampleRequest { CompanyId = company, CustomerId = mine.CustomerId, IsActive = true },
            new SampleRequest { CompanyId = company, CustomerId = unrelated.CustomerId, IsActive = true },
            new SampleRequest { CompanyId = Guid.NewGuid(), CustomerId = mine.CustomerId, IsActive = true }
        }.AsQueryable();
        var service = new CustomerVisibilityService(null!, null!, null!);
        var scope = new ViewerScope(company, employee, fullView, false, new HashSet<Guid>(),
            new HashSet<Guid> { employee }, DateTime.Now);
        var result = service.ApplySampleRequestVisibility(rows, new[] { mine, unrelated }.AsQueryable(), scope).ToArray();
        Assert.Equal(fullView ? 2 : 1, result.Length);
        Assert.All(result, row => Assert.Equal(company, row.CompanyId));
    }

    [Fact]
    public void InvalidFilterIdsAndOversizedSelectionsAreRejected()
    {
        var invalid = new SampleRequestSummaryFilters { EmployeeIds = [Guid.Empty] };
        Assert.NotEmpty(invalid.Validate(new ValidationContext(invalid)));
        var oversized = new SampleRequestSummaryFilters { ColourCodes = Enumerable.Repeat("BB", 21).ToArray() };
        Assert.NotEmpty(oversized.Validate(new ValidationContext(oversized)));
        var suggestion = new GetSampleRequestSummarySuggestionsQuery { Q = "x" };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(suggestion, new ValidationContext(suggestion), errors, true));
    }
}
