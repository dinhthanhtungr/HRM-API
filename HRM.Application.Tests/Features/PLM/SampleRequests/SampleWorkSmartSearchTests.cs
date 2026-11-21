using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkSmartSearchTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("End user: thanh nam")]
    [InlineData("Người dùng cuối: thanh nam")]
    [InlineData("endUser: thanh nam")]
    public void EndUserPrefixAndBadgeKeepActiveProductAndCompanyScope(string keyword)
    {
        var company = Guid.NewGuid();
        SampleRequestSampleTrialReportRow Row(string name, Guid productCompany, bool active = true) => new() {
            SampleRequest = new HRM.Domain.Entities.SampleRequestSchema.SampleRequest {
                CompanyId = company, Product = new HRM.Domain.Entities.SampleRequestSchema.Product {
                    CompanyId = productCompany, IsActive = active, EndUser = name
                }
            }
        };
        var rows = new[] { Row("Nam Thành", company), Row("Thành Nam", company), Row("Nam Thành", Guid.NewGuid()), Row("Nam Thành", company, false) }.AsQueryable();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var schema = SampleWorkSmartSearch.Build(rows, db);
        Assert.Equal(2, schema.Apply(rows, keyword, null, out var legacy).Count());
        Assert.Null(legacy);
        Assert.Single(schema.Apply(rows, null, "[{\"field\":\"endUser\",\"value\":\"Nam Thành\"}]", out _));
        var scoped = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
            .Select(x => new SampleRequestSampleTrialReportRow { SampleRequest = x });
        var sql = SampleWorkSmartSearch.Build(scoped, db).BuildSuggestionQuery(scoped,
            new GetSampleRequestSampleTrialsQuery { SuggestionKeyword = keyword })!.ToQueryString();
        Assert.Contains("endUser", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyId", sql); Assert.Contains("IsActive", sql);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT @").Count);
    }
    [Theory]
    [InlineData(SampleRequestWorkBucket.All, true)]
    [InlineData(SampleRequestWorkBucket.WaitingLab, false)]
    [InlineData(SampleRequestWorkBucket.WaitingPrice, false)]
    [InlineData(SampleRequestWorkBucket.ActionRequired, false)]
    public void OnlyAllBucketSuggestionsCanSkipDecisionFacts(SampleRequestWorkBucket bucket, bool expected)
        => Assert.Equal(expected, SampleWorkSmartSearch.CanReadMetadataOnly(new() {
            SuggestionKeyword = "Nam thành", WorkAudience = SampleRequestWorkAudience.Lab,
            WorkBucket = bucket, DailyWorkView = SampleRequestDailyView.All
        }));

    [Fact]
    public void ListsHistoryAndLegacyDailyViewsKeepExistingPipeline()
    {
        var request = new GetSampleRequestSampleTrialsQuery { WorkAudience = SampleRequestWorkAudience.Lab, WorkBucket = SampleRequestWorkBucket.All };
        Assert.False(SampleWorkSmartSearch.CanReadMetadataOnly(request));
        Assert.False(SampleWorkSmartSearch.CanReadMetadataOnly(new() { SuggestionKeyword = "Nam", IncludeTrialHistory = true, WorkAudience = SampleRequestWorkAudience.Lab }));
        Assert.False(SampleWorkSmartSearch.CanReadMetadataOnly(new() { SuggestionKeyword = "Nam", WorkAudience = SampleRequestWorkAudience.Lab, DailyWorkView = SampleRequestDailyView.Tracking }));
        Assert.False(SampleWorkSmartSearch.CanReadMetadataOnly(new() { SuggestionKeyword = "Nam" }));
    }

    [Fact]
    public void MetadataSqlPreservesNamesSnapshotsTenantAndOtherBadgesWithoutPriceDecisionProjection()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        _ = db.Model;
        var company = Guid.NewGuid();
        var rows = from request in db.SampleRequests.Where(x => x.IsActive && x.CompanyId == company)
            join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
            from trial in trials.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial, TrialCount = null };
        var metadata = SampleRequestLabWorkQuery.Enrich(rows, db);
        var requestQuery = new GetSampleRequestSampleTrialsQuery { SuggestionKeyword = "Nam thành",
            SearchSelections = "[{\"field\":\"customerName\",\"value\":\"Nam Thành\"},{\"field\":\"colourCode\",\"value\":\"BB01\"}]" };
        var watch = Stopwatch.StartNew();
        var legacy = SampleWorkDecisionQuery.Enrich(metadata, db, db, "VND");
        var oldSql = SampleWorkSmartSearch.Build(legacy, db).BuildSuggestionQuery(legacy, requestQuery)!.ToQueryString();
        var oldMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var sql = SampleWorkSmartSearch.Build(metadata, db).BuildSuggestionQuery(metadata, requestQuery)!.ToQueryString();
        output.WriteLine($"Local SQL translation only (not API/DB latency): previous={oldMs:F1}ms, metadata={watch.Elapsed.TotalMilliseconds:F1}ms; SQL chars={oldSql.Length}/{sql.Length}");
        Assert.Contains("CompanyId", sql); Assert.Contains("CustomerNameSnapshot", sql);
        Assert.Contains("BB01", sql); Assert.Contains("~*", sql);
        Assert.DoesNotContain("StandardSellingPrice", sql); Assert.DoesNotContain("MaterialCostSnapshot", sql);
        Assert.Equal(9, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT @").Count);
        var nameOnly = SampleWorkSmartSearch.Build(metadata, db).BuildSuggestionQuery(metadata,
            new GetSampleRequestSampleTrialsQuery { SuggestionKeyword = "Tên KH: Nam thành", SearchSelections = "[{\"field\":\"customerName\",\"value\":\"PREVIOUS_CUSTOMER\"}]" })!.ToQueryString();
        Assert.DoesNotContain("PREVIOUS_CUSTOMER", nameOnly);
    }
}
