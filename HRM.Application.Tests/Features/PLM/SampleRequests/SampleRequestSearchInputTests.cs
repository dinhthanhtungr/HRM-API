using System.Text.RegularExpressions;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSearchInputTests
{
    [Theory]
    [InlineData("Tên KH: Thành nam", "customer", "Thành nam")]
    [InlineData("  TEN   KH : Thành nam  ", "customer", "Thành nam")]
    [InlineData("Mã KH: CC", "customerCode", "CC")]
    [InlineData("Nhân viên: Hiền", "employee", "Hiền")]
    [InlineData("ColorCode: BB", "colourCode", "BB")]
    [InlineData("Tên sản phẩm: Hạt phụ gia", "product", "Hạt phụ gia")]
    [InlineData("TP Code: TP_1", "sampleRequest", "TP_1")]
    [InlineData("VU: VU_1", "formula", "VU_1")]
    [InlineData("Nhóm: Sale", "group", "Sale")]
    [InlineData("Customer Name： Nam", "customer", "Nam")]
    [InlineData("Tên KH:", "customer", "")]
    [InlineData("ABC: CC", null, "ABC: CC")]
    [InlineData("Thành nam", null, "Thành nam")]
    public void ParsesOnlyKnownLabels(string raw, string? type, string keyword)
    {
        var input = SampleRequestSearchInput.Parse(raw);
        Assert.Equal(type, input.Type); Assert.Equal(keyword, input.Keyword);
    }

    [Theory]
    [InlineData("Thành nam", "Nam Thành", true)]
    [InlineData("thành thanh", "Nam Thành", true)]
    [InlineData("hien tran", "Trần Thanh Hiền", true)]
    [InlineData("hien tran", "Trần Thanh", false)]
    [InlineData("nam thanh", "Nam\nThành", true)]
    [InlineData("nam %_", "Nam %_", true)]
    [InlineData("nam %_", "Nam ABC", false)]
    [InlineData("nam (.*)", "Nam ABC", false)]
    public void RequiresAllLiteralWordsInTheSameName(string input, string name, bool expected)
        => Assert.Equal(expected, Regex.IsMatch(name, SampleRequestSearchText.AllWordsPattern(input), RegexOptions.IgnoreCase));

    [Theory]
    [InlineData("nam thanh", "Nam\nThành")]
    [InlineData("thanh nam", "Nam\nThành")]
    [InlineData("nam (.*)", "Nam (.*)")]
    public void NamePatternsRemainValidWithProviderNewlineOptions(string keyword, string name)
    {
        var all = SampleRequestSearchText.AllWordsPattern(keyword);
        var ordered = SampleRequestSearchText.OrderedPattern(keyword);
        Assert.DoesNotContain("(?s)", all);
        Assert.DoesNotContain("(?s)", ordered);
        Assert.True(Regex.IsMatch(name, all, RegexOptions.IgnoreCase));
    }

    [Fact]
    public void ScopedSuggestionsBuildOneBranchAndFreeSearchUsesTheSameField()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var visible = db.SampleRequests.Where(x => x.IsActive && x.CompanyId == Guid.Empty);
        var sql = SummarySuggestionQueries.Build(db, visible, "Tên KH: Thành nam").ToQueryString();
        Assert.DoesNotContain("UNION ALL", sql); Assert.Single(Regex.Matches(sql, "LIMIT"));
        Assert.Contains("~*", sql); Assert.DoesNotContain("ILIKE", sql);
        var codeSql = SummarySuggestionQueries.Build(db, visible, "Mã KH: CC").ToQueryString();
        Assert.DoesNotContain("UNION ALL", codeSql); Assert.DoesNotContain("~*", codeSql);
        Assert.Contains("ILIKE", codeSql);
        var freeSql = SampleRequestSummaryKeywordSearch.Apply(visible, db, "Tên KH: Thành nam").ToQueryString();
        Assert.Contains("~*", freeSql); Assert.DoesNotContain("ILIKE", freeSql);
        var scopedCode = SampleRequestSummaryKeywordSearch.Apply(visible, db, "TP Code: TP_1").ToQueryString();
        Assert.Contains("ILIKE", scopedCode); Assert.DoesNotContain("~*", scopedCode);
    }

    [Fact]
    public void OrderedNamesRankBeforeReversedNames()
    {
        Assert.True(Regex.IsMatch("Nam Thành", SampleRequestSearchText.OrderedPattern("nam thanh"), RegexOptions.IgnoreCase));
        Assert.False(Regex.IsMatch("Nam Thành", SampleRequestSearchText.OrderedPattern("thanh nam"), RegexOptions.IgnoreCase));
        Assert.True(Regex.IsMatch("Trần Thanh Hiền", SampleRequestSearchText.OrderedPattern("tran hien"), RegexOptions.IgnoreCase));
    }

    [Theory]
    // EF lặp biểu thức rank ở SELECT và ORDER BY: 1 WHERE + 2 lần rank.
    [InlineData("Hiền", 5)]
    [InlineData("Thành nam", 7)]
    public void SingleWordSqlOmitsRedundantOrderedMatch(string keyword, int expectedRegexChecks)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SummarySuggestionQueries.Build(db, db.SampleRequests.Where(x => x.IsActive),
            "Tên KH: " + keyword).ToQueryString();
        Assert.Equal(expectedRegexChecks, Regex.Matches(sql, "~\\*").Count);
    }

    [Fact]
    public void SingleWordOptimizationPreservesRankingAndTopTen()
    {
        var names = new[] { "Hiền", "Hiền Nguyễn", "Trần Thanh Hiền", "Không khớp" };
        var candidates = names.Select((name, index) => new SummarySuggestionQueries.Candidate
            { Type = "employee", Value = index.ToString(), Label = name }).AsQueryable();
        var result = SummarySuggestionQueries.MatchText(candidates, "Hiền",
            SampleRequestSearchText.PhrasePattern("Hiền")).ToArray();
        Assert.Equal(new[] { "Hiền", "Hiền Nguyễn", "Trần Thanh Hiền" }, result.Select(x => x.Label));
        Assert.Equal(new[] { 0, 1, 2 }, result.Select(x => x.MatchRank));
    }
}
