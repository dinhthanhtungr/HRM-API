using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Services.Search;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Chỉ SELECT tham số trên cluster test cô lập đã có; không tạo schema hoặc đọc DB HRM thật.</summary>
public sealed class SampleRequestNameSearchPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task PostgresSupportsTheActualLiteralWordPatternsIncludingReorderingAndNewlines()
    {
        await using var connection = await postgres.OpenAsync();
        foreach (var (query, name, expected) in new[] {
            ("Thành nam", "Nam Thành", true),
            ("hien tran", "Trần Thanh Hiền", true),
            ("hien tran", "Trần Thanh", false),
            ("nam thanh", "Nam\nThành", true),
            ("nam %_", "Nam %_", true),
            ("nam %_", "Nam ABC", false),
            ("nam (.*)", "Nam ABC", false)
        })
        {
            await using var command = new NpgsqlCommand("SELECT @name ~* @pattern", connection);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("pattern", SampleRequestSearchText.AllWordsPattern(query));
            Assert.Equal(expected, (bool)(await command.ExecuteScalarAsync())!);
        }
    }
    [PostgresGroupStorageFact]
    public async Task SuggestionsExecuteProviderTranslatedRegexIncludingNewlinesAndLiteralSymbols()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).Options);
        foreach (var (keyword, name, expectedRank) in new[] {
            ("nam thanh", "Nam Thành", 0),
            ("thanh nam", "Nam Thành", 3),
            ("nam thanh", "Nam\nThành", 0),
            ("nam hien", "Nam\nThanh Hiền", 2),
            ("nam (.*)", "Nam (.*)", 0)
        })
        {
            var candidates = db.Customers.FromSql($"SELECT {name}::citext AS \"CustomerName\"")
                .Select(customer => new SummarySuggestionQueries.Candidate
                    { Type = "customer", Value = "1", Label = customer.CustomerName, Code = "" });
            var rows = await SummarySuggestionQueries.MatchText(candidates, keyword,
                SampleRequestSearchText.PhrasePattern(keyword)).ToListAsync();
            Assert.Equal(expectedRank, Assert.Single(rows).MatchRank);
        }
    }
}
