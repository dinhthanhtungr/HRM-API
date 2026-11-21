using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleNewestFactsPageTests
{
    [Theory]
    [MemberData(nameof(SampleWorkDecisionTests.Cases), MemberType = typeof(SampleWorkDecisionTests))]
    public async Task CanonicalLabFilterRunsBeforeRequestCountAndPaging(string name, object input, string code, string role, string stage)
    {
        Assert.NotEmpty(name);
        var facts = (SampleWorkFacts)input;
        Assert.Equal(new SampleWorkDecision(stage, code, role), SampleWorkDecisionRules.Resolve(facts));
        var rows = Enumerable.Range(0, 3).Select(_ => new SampleRequestDailyPriorityFactsPage.ReadRow
        {
            State = new(Guid.NewGuid(), null, facts, null, null, false)
        }).ToArray();
        var result = await SampleRequestNewestFactsPage.FromRowsAsync(Stream(rows),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), 2, 1, default);
        var matches = facts.Open && role == "Lab";
        Assert.Equal(matches ? 3 : 0, result.TotalCount);
        Assert.Equal(matches ? new[] { rows[1].State.RequestId } : [], result.Payload.States.Select(x => x.RequestId));
    }

    private static async IAsyncEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> Stream(
        IEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> rows)
    {
        foreach (var row in rows) { yield return row; await Task.CompletedTask; }
    }
}
