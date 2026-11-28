using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceWaitingQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedCountsAndBucketsWaitForPriceWithoutChangingSendDecision(bool accepted)
    {
        var facts = new SampleWorkFacts { Open = true, RequestedKg = true, RequiresPriceAcceptance = true,
            AcceptedPrice = accepted, FormulaStatus = "Approved", LabConfirmed = true, Quotation = true };
        var decision = SampleWorkDecisionRules.Resolve(facts);
        Assert.Equal("SendSample", decision.Code); Assert.Equal("Lab", decision.Role);
        Assert.Equal(!accepted, SampleWorkDecisionRules.WaitsForPriceBeforeSample(facts, decision.Code));
        Assert.Equal(accepted, SampleWorkTaskPriority.Tasks(facts, decision, SampleRequestWorkAudience.Lab, false, "InProgress").Contains("SendSample"));
        var row = new SampleRequestDailyPriorityFactsPage.ReadRow { CustomerId = Guid.NewGuid(), Status = "InProgress",
            State = new(Guid.NewGuid(), null, facts, Guid.NewGuid(), null, false) };
        var raw = Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable();
        foreach (var bucket in new[] { SampleRequestWorkBucket.ActionRequired, SampleRequestWorkBucket.WaitingSale, SampleRequestWorkBucket.WaitingLab, SampleRequestWorkBucket.All })
        {
            var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, false, [], Bucket: bucket);
            var page = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(row), raw, access, DateTime.Today, 1, 20, default);
            var expected = bucket == SampleRequestWorkBucket.All || (bucket == SampleRequestWorkBucket.WaitingSale ? !accepted : accepted);
            Assert.Equal(expected ? 1 : 0, page.TotalCustomers);
            if (expected && page.TaskCounts is not null) Assert.Equal(accepted ? 1 : 0, page.TaskCounts["SendSample"]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UngroupedQueueUsesSameWaitingClassification(bool waiting)
    {
        var row = new SampleRequestSampleTrialReportRow { SampleRequest = new SampleRequest { Status = "InProgress" },
            WorkDecision = new("ReadyToSendSample", "SendSample", "Lab"), WaitsForPriceBeforeSample = waiting };
        foreach (var bucket in new[] { SampleRequestWorkBucket.ActionRequired, SampleRequestWorkBucket.WaitingSale, SampleRequestWorkBucket.WaitingLab })
        {
            var query = new GetSampleRequestSampleTrialsQuery { WorkAudience = SampleRequestWorkAudience.Lab, WorkBucket = bucket };
            var count = SampleRequestWorkQueueQuery.Apply(new[] { row }.AsQueryable(), query, null!).Count();
            Assert.Equal((bucket == SampleRequestWorkBucket.WaitingSale ? waiting : !waiting) ? 1 : 0, count);
        }
    }

    [Fact]
    public void NonPriceAndChipAndLegacyAndInternalWorkRemainUnchanged()
    {
        var basis = new SampleWorkFacts { Open = true, RequestedKg = true, RequiresPriceAcceptance = true, FormulaStatus = "Approved" };
        foreach (var facts in new[] { basis with { RequiresPriceAcceptance = false }, basis with { RequestedKg = false },
            basis with { Legacy = true }, basis with { InternalDirect = true }, basis with { Open = false } })
            Assert.False(SampleWorkDecisionRules.WaitsForPriceBeforeSample(facts, SampleWorkDecisionRules.Resolve(facts).Code));
    }

    private static async IAsyncEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> Stream(SampleRequestDailyPriorityFactsPage.ReadRow row)
    { yield return row; await Task.CompletedTask; }
}
