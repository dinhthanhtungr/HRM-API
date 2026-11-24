using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleDailyPriorityFactsPageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizationFilterExcludesOrdinaryConfirmation(bool grouped)
    {
        var ordinary = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" }, "InProgress", false, null);
        var final = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Approved", LabFinalizationRequested = true,
            AcceptedPrice = true, Quotation = true }, "InProgress", false, null);
        Assert.Equal("FinalizeQuotation", SampleWorkDecisionRules.Resolve(final.State.Facts).Code);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab,
            true, false, false, false, [], "Lab", SampleRequestWorkBucket.ActionRequired,
            PriorityTasks: ["FinalizeFormula"], GroupByCustomer: grouped);
        var page = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([ordinary, final]),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), access, DateTime.Today, 1, 1, default);
        Assert.Equal(1, page.TaskCounts!["FinalizeFormula"]);
        Assert.Equal(1, page.TaskCounts["ApproveFormula"]);
        Assert.Equal(final.State.RequestId, Assert.Single(page.Payload.States).RequestId);
        Assert.Equal(1, page.TotalCustomers);
    }

    [Theory]
    [InlineData("FinalizeQuotation")]
    [InlineData("FinalizeChip")]
    [InlineData("ApproveFormula")]
    public void StatusChangePriorityMeansFinalizationNotOrdinaryApproval(string code)
    {
        var tasks = SampleWorkTaskPriority.Tasks(new() { Open = true },
            new() { Role = "Lab", Code = code }, SampleRequestWorkAudience.Lab, false, "InProgress");
        Assert.Equal(code != "ApproveFormula", tasks.Contains("FinalizeFormula"));
        Assert.Contains(code, tasks); // Existing individual priorities remain compatible.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewStatusHasSeparateCountsAndPriorityWithoutRemovingOtherRequests(bool grouped)
    {
        var pending = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" }, "InProgress", false, null);
        var fresh = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" }, "New", false, null);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab,
            true, false, false, false, [], "Lab", SampleRequestWorkBucket.ActionRequired,
            PriorityTasks: ["NewRequest"], GroupByCustomer: grouped);
        var raw = Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable();
        var first = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([pending, fresh]), raw, access, DateTime.Today, 1, 1, default);
        var second = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([pending, fresh]), raw, access, DateTime.Today, 2, 1, default);
        Assert.Equal(1, first.TaskCounts!["NewRequest"]);
        Assert.Equal(2, first.TaskCounts["ApproveFormula"]);
        Assert.Equal(fresh.State.RequestId, Assert.Single(first.Payload.States).RequestId);
        Assert.Equal(pending.State.RequestId, Assert.Single(second.Payload.States).RequestId);
        Assert.Equal(2, first.TotalCustomers);
    }

    [Theory]
    [InlineData(SampleRequestWorkAudience.Lab)]
    [InlineData(SampleRequestWorkAudience.LabAdmin)]
    public void NewIsAStatusNotAnUnconfirmedFormulaOrCreationDate(SampleRequestWorkAudience audience)
    {
        var facts = new SampleWorkFacts { Open = true, FormulaStatus = "Draft" };
        var decision = SampleWorkDecisionRules.Resolve(facts);
        Assert.Equal(new[] { "NewRequest", "ApproveFormula" }, SampleWorkTaskPriority.Tasks(facts, decision, audience, false, "New"));
        Assert.Equal(new[] { "ApproveFormula" }, SampleWorkTaskPriority.Tasks(facts, decision, audience, false, "InProgress"));
        var closed = facts with { Open = false };
        Assert.Empty(SampleWorkTaskPriority.Tasks(closed, SampleWorkDecisionRules.Resolve(closed), audience, false, "Completed"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LabTaskFiltersBeforePagingAndSupportsMultipleSelections(bool grouped)
    {
        var ordinary = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" }, "InProgress", false, null);
        var preferred = Row(ordinary.CustomerId, new() { Open = true, FormulaStatus = "Approved", RequestedKg = true }, "InProgress", false, null);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab,
            true, false, false, false, [], "Lab", SampleRequestWorkBucket.ActionRequired,
            PriorityTasks: ["SendSample"], GroupByCustomer: grouped);
        var raw = Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable();
        var first = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([ordinary, preferred]), raw, access, DateTime.Today, 1, 1, default);
        var second = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([ordinary, preferred]), raw, access, DateTime.Today, 2, 1, default);
        Assert.Equal(preferred.State.RequestId, Assert.Single(first.Payload.States).RequestId);
        Assert.Empty(second.Payload.States);
        Assert.Equal(1, first.TotalCustomers);
        Assert.Equal(1, first.TaskCounts!["SendSample"]);
        Assert.Equal(1, first.TaskCounts["ApproveFormula"]);
        var combined = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([ordinary, preferred]), raw,
            access with { PriorityTasks = ["SendSample", "ApproveFormula"] }, DateTime.Today, 1, 2, default);
        Assert.Equal(grouped ? 1 : 2, combined.TotalCustomers);
        Assert.Equal(2, combined.Payload.States.Count);

        var unfiltered = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([ordinary, preferred]), raw,
            access with { PriorityTasks = [] }, DateTime.Today, 1, 2, default);
        Assert.Equal(2, unfiltered.Payload.States.Count);
    }

    [Fact]
    public void SalePriceAndSampleTasksRemainIndependentAndUnknownPrioritiesAreIgnored()
    {
        var facts = new SampleWorkFacts { Open = true, LabConfirmed = true, ApprovedPrice = true,
            FormulaStatus = "SampleSent", PendingTrial = true, Kg = true };
        var tasks = SampleWorkTaskPriority.Tasks(facts, SampleWorkDecisionRules.Resolve(facts), SampleRequestWorkAudience.Sale, false, "InProgress");
        Assert.Contains("PriceFeedback", tasks);
        Assert.Contains("SampleFeedback", tasks);
        Assert.Equal(new[] { "SendSample" }, SampleWorkTaskPriority.Normalize(["unknown", "SendSample", "SendSample"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaleSelectionFiltersRowsAndEmptySelectionStillSorts(bool grouped)
    {
        var resume = Row(Guid.NewGuid(), new() { Open = true, LabPaused = true }, "InProgress", true, null);
        var sample = Row(resume.CustomerId, new() { Open = true, PendingTrial = true, Kg = true },
            "InProgress", true, null);
        var raw = Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable();
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Sale,
            false, true, false, false, [], "Sale", SampleRequestWorkBucket.MyActionRequired, GroupByCustomer: grouped);
        var rows = new[] { resume, sample };
        var filtered = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), raw,
            access with { PriorityTasks = ["ResumeAfterLabRejection"] }, DateTime.Today, 1, 1, default);
        Assert.Equal(resume.State.RequestId, Assert.Single(filtered.Payload.States).RequestId);
        Assert.Equal(1, filtered.TotalCustomers);
        Assert.Equal(1, filtered.TaskCounts!["SampleFeedback"]);

        var sorted = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), raw,
            access, DateTime.Today, 1, 2, default);
        Assert.Equal(new[] { sample.State.RequestId, resume.State.RequestId }, sorted.Payload.States.Select(x => x.RequestId));
        Assert.Equal(grouped ? 1 : 2, sorted.TotalCustomers);

        var combined = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), raw,
            access with { PriorityTasks = ["ResumeAfterLabRejection", "SampleFeedback"] }, DateTime.Today, 1, 2, default);
        Assert.Equal(2, combined.Payload.States.Count);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("BP", false)]
    public void SelectedSmartFilterKeepsFactsPagingWhileSuggestionsKeepTheirOwnPipeline(string? suggestion, bool enabled)
    {
        var request = new GetSampleRequestSampleTrialsQuery {
            GroupByCustomer = true, WorkSort = SampleRequestWorkSort.WorkPriority,
            WorkAudience = SampleRequestWorkAudience.Lab, WorkBucket = SampleRequestWorkBucket.All,
            SearchSelections = "[{\"field\":\"colourCode\",\"value\":\"BP51076\"}]", SuggestionKeyword = suggestion };
        Assert.Equal(enabled, SampleRequestDailyPriorityFactsPage.CanRead(request));
    }
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void BoundedUngroupedPriorityRefreshUsesFactsPaging(int requestCount, bool expected)
    {
        var request = new GetSampleRequestSampleTrialsQuery
        {
            WorkAudience = SampleRequestWorkAudience.Sale,
            WorkBucket = SampleRequestWorkBucket.All,
            WorkSort = SampleRequestWorkSort.WorkPriority,
            GroupByCustomer = false,
            SampleRequestIds = Enumerable.Range(0, requestCount).Select(_ => Guid.NewGuid()).ToArray()
        };

        Assert.Equal(expected, SampleRequestDailyPriorityFactsPage.CanRead(request));
    }

    [Fact]
    public async Task PinnedCustomerAtEndOfStreamIsSelectedBeforeEarlierCustomers()
    {
        var facts = new SampleWorkFacts();
        var first = Row(Guid.NewGuid(), facts, SampleRequestStatus.InProgress.ToString(), true, null);
        var pinned = Row(Guid.NewGuid(), facts, SampleRequestStatus.Completed.ToString(), true, null);
        var access = new SampleRequestDailyPriorityFactsPage.Access(
            SampleRequestWorkAudience.Lab, true, false, false, false, [], null,
            SampleRequestWorkBucket.All, [pinned.State.RequestId]);
        var page = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([first, pinned]),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), access, new(2026, 11, 7), 1, 1, CancellationToken.None);
        Assert.Equal(pinned.CustomerId, Assert.Single(page.CustomerIds));
        Assert.Equal(2, page.TotalCustomers);
    }

    [Theory]
    [MemberData(nameof(SampleWorkDecisionTests.Cases), MemberType = typeof(SampleWorkDecisionTests))]
    public async Task WorkFilterRunsBeforeGroupCountAndPaging(string name, object input, string code, string role, string stage)
    {
        Assert.False(string.IsNullOrEmpty(name));
        Assert.Equal(new SampleWorkDecision(stage, code, role), SampleWorkDecisionRules.Resolve((SampleWorkFacts)input));
        foreach (var required in new[] { "Sale", "Lab" })
        {
            var facts = (SampleWorkFacts)input;
            var customer = Guid.NewGuid();
            var rows = new[] { Row(customer, facts, facts.Open ? "InProgress" : "Completed", true, null),
                Row(customer, facts, facts.Open ? "InProgress" : "Completed", true, null) };
            var access = new SampleRequestDailyPriorityFactsPage.Access(
                required == "Sale" ? SampleRequestWorkAudience.Sale : SampleRequestWorkAudience.Lab,
                true, true, false, false, [], required);
            var page = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows),
                Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), access, new(2026, 11, 7), 1, 15, CancellationToken.None);
            var expected = facts.Open && (role == required || required == "Sale" &&
                (SampleWorkDecisionRules.HasSalePriceWork(facts) || SampleWorkDecisionRules.HasSaleFeedbackWork(facts)));
            Assert.Equal(expected ? 1 : 0, page.TotalCustomers);
            Assert.Equal(expected ? 2 : 0, page.Payload.States.Count);
        }
    }

    [Theory]
    [InlineData(SampleRequestWorkAudience.Sale, SampleRequestWorkBucket.MyActionRequired, true)]
    [InlineData(SampleRequestWorkAudience.Sale, SampleRequestWorkBucket.ActionRequired, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.ActionRequired, true)]
    [InlineData(SampleRequestWorkAudience.LabAdmin, SampleRequestWorkBucket.ActionRequired, true)]
    [InlineData(SampleRequestWorkAudience.Sale, SampleRequestWorkBucket.WaitingLab, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.Rework, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.NewRequestRequired, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.FeedbackReceived, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.Completed, true)]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.Cancelled, true)]
    public void OnlyCompatibleQueuesUseFactsPaging(SampleRequestWorkAudience audience, SampleRequestWorkBucket bucket, bool expected)
    {
        Assert.Equal(expected, SampleRequestDailyPriorityFactsPage.CanRead(new()
            { GroupByCustomer = true, WorkSort = SampleRequestWorkSort.WorkPriority, WorkAudience = audience, WorkBucket = bucket }));
        Assert.Equal(bucket is SampleRequestWorkBucket.ActionRequired or SampleRequestWorkBucket.MyActionRequired,
            SampleRequestDailyPriorityFactsPage.CanRead(new()
            { GroupByCustomer = false, WorkSort = SampleRequestWorkSort.WorkPriority, WorkAudience = audience, WorkBucket = bucket }));
    }

    [Theory]
    [InlineData(SampleRequestWorkBucket.Completed, "Completed", false, 1)]
    [InlineData(SampleRequestWorkBucket.Completed, "InProgress", false, 0)]
    [InlineData(SampleRequestWorkBucket.Cancelled, "Cancelled", false, 1)]
    [InlineData(SampleRequestWorkBucket.Cancelled, "Completed", false, 0)]
    [InlineData(SampleRequestWorkBucket.FeedbackReceived, "Completed", true, 1)]
    [InlineData(SampleRequestWorkBucket.FeedbackReceived, "InProgress", true, 1)]
    [InlineData(SampleRequestWorkBucket.FeedbackReceived, "InProgress", false, 0)]
    public async Task FeedbackAndClosedBucketsFilterBeforeCountWithoutRequiringOpenWork(
        SampleRequestWorkBucket bucket, string status, bool feedback, int expected)
    {
        var row = new SampleRequestDailyPriorityFactsPage.ReadRow
        {
            CustomerId = Guid.NewGuid(), Status = status, HasFeedback = feedback,
            State = new(Guid.NewGuid(), null, new() { Open = status == "InProgress" }, null, null, false)
        };
        var result = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([row]),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(),
            new(SampleRequestWorkAudience.Lab, true, false, false, false, [], Bucket: bucket),
            new(2026, 11, 7), 1, 15, CancellationToken.None);
        Assert.Equal(expected, result.TotalCustomers);
        Assert.Equal(expected, result.Payload.States.Count);
    }

    [Theory]
    [MemberData(nameof(SampleWorkDecisionTests.Cases), MemberType = typeof(SampleWorkDecisionTests))]
    public void AdditionalBucketsMatchExistingSqlPredicates(string name, object input, string code, string role, string stage)
    {
        Assert.False(string.IsNullOrEmpty(name));
        var facts = (SampleWorkFacts)input;
        var decision = new SampleWorkDecision(stage, code, role);
        var day = new DateTime(2026, 11, 7);
        var row = Row(Guid.NewGuid(), facts, facts.Open ? "InProgress" : "Completed", true, day.AddDays(-1));
        var sqlRow = new SampleRequestSampleTrialReportRow
        {
            SampleRequest = new SampleRequest { Status = row.Status, InfoType = facts.Quotation ? "quotation" : "chip",
                SampleQuantity = facts.RequestedKg ? 2 : 0, ExpectedPriceQuoteDate = row.Due, ExpectedDeliveryDate = row.Due },
            WorkDecision = decision, HasSalePriceWork = SampleWorkDecisionRules.HasSalePriceWork(facts),
            HasSaleFeedbackWork = SampleWorkDecisionRules.HasSaleFeedbackWork(facts)
        };
        foreach (var bucket in new[] { SampleRequestWorkBucket.Rework, SampleRequestWorkBucket.NewRequestRequired,
            SampleRequestWorkBucket.WaitingLab, SampleRequestWorkBucket.WaitingSale, SampleRequestWorkBucket.WaitingPrice,
            SampleRequestWorkBucket.LabProgress, SampleRequestWorkBucket.Overdue })
        {
            var expected = SampleRequestWorkQueueQuery.Apply(new[] { sqlRow }.AsQueryable(),
                new() { WorkBucket = bucket, DailyWorkDate = DateOnly.FromDateTime(day) }, null!).Any();
            Assert.Equal(expected, SampleRequestDailyPriorityFactsPage.MatchesBucket(row, decision, bucket, day));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaleOwnershipDoesNotReplaceWorkflowCapability(bool saleCapability)
    {
        var facts = new SampleWorkFacts { Open = true, Quotation = true, LabConfirmed = true,
            FormulaStatus = "Approved", ApprovedPrice = true, Quoted = true };
        Assert.Equal("Sale", SampleWorkDecisionRules.Resolve(facts).Role);
        var first = Row(Guid.NewGuid(), facts, "InProgress", true, new(2026, 11, 9));
        var second = Row(Guid.NewGuid(), facts, "InProgress", false, new(2026, 11, 7));
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Sale, false, saleCapability, false, false, []);
        var result = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream([first, second]),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), access, new(2026, 11, 6), 1, 1, CancellationToken.None);
        Assert.Equal(saleCapability ? first.CustomerId : second.CustomerId, Assert.Single(result.CustomerIds));
    }

    [Theory]
    [MemberData(nameof(SampleWorkDecisionTests.Cases), MemberType = typeof(SampleWorkDecisionTests))]
    public async Task OnePassPriorityMatchesSqlRankForEveryDecision(string name, object input, string code, string role, string stage)
    {
        var facts = (SampleWorkFacts)input;
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(new SampleWorkDecision(stage, code, role), SampleWorkDecisionRules.Resolve(facts));
        foreach (var audience in new[] { SampleRequestWorkAudience.Lab, SampleRequestWorkAudience.Sale, SampleRequestWorkAudience.LabAdmin })
        {
            var owner = Guid.NewGuid();
            var access = new SampleRequestDailyPriorityFactsPage.Access(audience, audience == SampleRequestWorkAudience.Lab,
                audience == SampleRequestWorkAudience.Sale, true, audience == SampleRequestWorkAudience.LabAdmin, [owner]);
            var first = Row(Guid.NewGuid(), facts, facts.Open ? "InProgress" : "Completed", true, new DateTime(2026, 11, 5));
            var second = Row(Guid.NewGuid(), new() { Open = true, Quotation = true, LabConfirmed = true,
                FormulaStatus = "Approved", ApprovedPrice = true, Quoted = true }, "InProgress", false, null);
            var rows = new[] { first, second };
            var expected = rows.Select(x => new SampleRequestSampleTrialReportRow
            {
                SampleRequest = new() { SampleRequestId = x.State.RequestId, CustomerId = x.CustomerId, Status = x.Status,
                    ManagerBy = x.SaleWritable ? owner : Guid.NewGuid(), InfoType = x.State.Facts.Quotation ? "quotation" : "chip",
                    SampleQuantity = x.State.Facts.RequestedKg ? 2 : 0, ExpectedPriceQuoteDate = x.Due, ExpectedDeliveryDate = x.Due },
                WorkDecision = SampleWorkDecisionRules.Resolve(x.State.Facts),
                LabTaskRank = SampleWorkTaskPriority.ResolveLabRank(x.State.Facts, SampleWorkDecisionRules.Resolve(x.State.Facts).Code),
                HasSalePriceWork = SampleWorkDecisionRules.HasSalePriceWork(x.State.Facts),
                HasSaleFeedbackWork = SampleWorkDecisionRules.HasSaleFeedbackWork(x.State.Facts)
            }).AsQueryable();
            var ranked = SampleRequestWorkSortQuery.Enrich(expected, audience, new(2026, 11, 6), access.Technical,
                access.Sale, access.Pricing, access.Schedule, [owner], expected.Select(x => x.SampleRequest).Where(x => x.ManagerBy == owner));
            // Resolve ties in the supplied PostgreSQL customer order, rather than request UUID order.
            var expectedIds = ranked.ToArray().OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
                .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).Select(x => x.SampleRequest.CustomerId).ToArray();
            var result = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), expected, access,
                new(2026, 11, 6), 1, 15, CancellationToken.None);
            Assert.Equal(expectedIds, result.CustomerIds);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LabPriorityPutsOrderDeliveryAndExistingFormulasBeforePreparation(bool grouped)
    {
        var due = new DateTime(2026, 11, 1);
        var prepare = Row(Guid.NewGuid(), new() { Open = true }, "New", false, due);
        var approve = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" }, "InProgress", false, null);
        var chip = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Approved" }, "InProgress", false, null);
        var kg = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Approved", RequestedKg = true }, "InProgress", false, null);
        var finish = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Approved", Quotation = true,
            LabFinalizationRequested = true, AcceptedPrice = true }, "InProgress", false, null);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, false, [],
            GroupByCustomer: grouped);
        var rows = new[] { prepare, approve, chip, kg, finish };
        var raw = Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable();
        var first = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), raw, access, due.AddDays(1), 1, 4, default);
        var rest = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), raw, access, due.AddDays(1), 2, 4, default);
        Assert.Equal(new[] { finish.State.RequestId, kg.State.RequestId, chip.State.RequestId, approve.State.RequestId },
            first.Payload.States.Select(x => x.RequestId));
        Assert.Equal(prepare.State.RequestId, Assert.Single(rest.Payload.States).RequestId);
    }

    [Fact]
    public async Task LaterUrgentCustomerKeepsEveryTrialAndEvictsOnlyWholeLowerPriorityGroups()
    {
        var facts = new SampleWorkFacts { Open = true };
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var rows = new[] { Row(a, facts, "InProgress", false, null), Row(a, facts, "InProgress", false, null),
            Row(b, facts, "InProgress", false, new(2026, 11, 9)),
            Row(c, facts, "InProgress", false, new(2026, 11, 4)), Row(c, facts, "InProgress", false, new(2026, 11, 10)) };
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, false, []);
        var result = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(),
            access, new(2026, 11, 6), 1, 1, CancellationToken.None);
        Assert.Equal(new[] { c }, result.CustomerIds);
        Assert.Equal(2, result.Payload.States.Count);
        Assert.Equal(rows.Skip(3).Select(x => x.State.RequestId), result.Payload.States.Select(x => x.RequestId));
        var page2 = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(),
            access, new(2026, 11, 6), 2, 1, CancellationToken.None);
        Assert.Equal(new[] { b }, page2.CustomerIds);
        Assert.Equal(3, page2.TotalCustomers);
        var empty = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(),
            access, new(2026, 11, 6), 4, 1, CancellationToken.None);
        Assert.Empty(empty.CustomerIds);
        Assert.Equal(3, empty.TotalCustomers);
        Assert.Empty(empty.Payload.States);
        var pastIntegerLimit = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows), Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(),
            access, new(2026, 11, 6), int.MaxValue, 15, CancellationToken.None);
        Assert.Empty(pastIntegerLimit.CustomerIds);
        Assert.Equal(3, pastIntegerLimit.TotalCustomers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchedulePermissionDoesNotPrioritizeManagementReviewInLabWork(bool canSchedule)
    {
        var review = Row(Guid.NewGuid(), new() { Open = true, HasTrial = true, UnknownSent = true },
            "InProgress", false, new(2026, 11, 1));
        var lab = Row(Guid.NewGuid(), new() { Open = true, FormulaStatus = "Draft" },
            "InProgress", false, new(2026, 11, 9));
        Assert.Equal("LabAdmin", SampleWorkDecisionRules.Resolve(review.State.Facts).Role);
        Assert.Equal("Lab", SampleWorkDecisionRules.Resolve(lab.State.Facts).Role);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, canSchedule, []);
        var rows = new[] { review, lab };
        var ordered = SampleRequestDailyPriorityFactsPage.OrderPageStates(rows, access, new(2026, 11, 6));
        Assert.Equal(lab.State.RequestId, ordered[0].RequestId);
        var page = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(Stream(rows),
            Array.Empty<SampleRequestSampleTrialReportRow>().AsQueryable(), access, new(2026, 11, 6), 1, 1, default);
        Assert.Equal(lab.CustomerId, Assert.Single(page.CustomerIds));
        Assert.Equal(2, page.TotalCustomers);
    }

    private static SampleRequestDailyPriorityFactsPage.ReadRow Row(Guid customer, SampleWorkFacts facts, string status,
        bool writable, DateTime? due) => new()
        {
            CustomerId = customer, Status = status, Due = due, SaleWritable = writable,
            State = new(Guid.NewGuid(), Guid.NewGuid(), facts, null, null, true)
        };

    private static async IAsyncEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> Stream(
        IEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> rows)
    {
        foreach (var row in rows) yield return row;
        await Task.CompletedTask;
    }
}
