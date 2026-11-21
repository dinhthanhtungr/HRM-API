using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkSortTests
{
    [Theory]
    [InlineData(SampleRequestWorkAudience.Lab)]
    [InlineData(SampleRequestWorkAudience.Sale)]
    [InlineData(SampleRequestWorkAudience.LabAdmin)]
    public void NewestSortOrdersRequestsBeforePagingAndOldPinsWin(SampleRequestWorkAudience audience)
    {
        var old = Row("old", "Lab", "New", null);
        var newest = Row("newest", "Sale", "New", null);
        var middle = Row("middle", "Lab", "InProgress", null);
        old.SampleRequest.CreatedDate = new(2026, 11, 1);
        middle.SampleRequest.CreatedDate = new(2026, 11, 5);
        newest.SampleRequest.CreatedDate = new(2026, 11, 11);
        var rows = new[] { old, newest, middle }.AsQueryable();
        var ordered = SampleRequestWorkSortQuery.Apply(rows, SampleRequestWorkSort.Newest, false, audience,
            new(2026, 11, 11), false, false, false, false, [], Array.Empty<SampleRequest>().AsQueryable());
        Assert.Equal(new[] { "newest", "middle", "old" }, ordered.Select(x => x.SampleRequest.ExternalId));
        Assert.Equal("middle", ordered.Skip(1).Take(1).Single().SampleRequest.ExternalId);
        Assert.Equal("old", SampleRequestWorkSortQuery.OrderNewest(rows, [old.SampleRequest.SampleRequestId])
            .Take(1).Single().SampleRequest.ExternalId);
    }
    [Fact]
    public void PinsPrecedeBothSortsAndCustomerPagingWithoutDuplicates()
    {
        var first = Row("A", "Lab", "InProgress", null);
        var pinned = Row("Z", "Sale", "Completed", null);
        pinned.WorkSortRank = 2;
        var rows = new[] { first, pinned }.AsQueryable();
        Guid[] pins = [pinned.SampleRequest.SampleRequestId];
        Assert.Equal(pinned, SampleRequestWorkSortQuery.OrderPriority(rows, pins).First());
        Assert.Equal(pinned, SampleRequestWorkSortQuery.OrderCustomerCode(rows, pins).First());
        Assert.Equal(pinned.SampleRequest.CustomerId,
            SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(rows, 1, 1, pins).Single());
        Assert.Equal(first.SampleRequest.CustomerId,
            SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(rows, 2, 1, pins).Single());
        var page = new SampleCustomerPriorityPage();
        foreach (var fact in SampleRequestCustomerPageQuery.PriorityFacts(rows, pins)) page.Include(fact);
        Assert.Equal(pinned.SampleRequest.CustomerId, page.GetIds(1, 1).Single());
        Assert.Equal(first.SampleRequest.CustomerId, page.GetIds(2, 1).Single());
        Assert.Equal(first.SampleRequest.CustomerId,
            SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(rows.Where(x => x != pinned), 1, 1, pins).Single());
    }

    [Theory]
    [InlineData("LabUser", SampleRequestWorkAudience.Lab, "Lab")]
    [InlineData("LabUser", SampleRequestWorkAudience.Lab, "Sale")]
    [InlineData("LabUser", SampleRequestWorkAudience.Lab, "LabAdmin")]
    [InlineData("LabUser", SampleRequestWorkAudience.LabAdmin, "LabAdmin")]
    [InlineData("LabAdmin", SampleRequestWorkAudience.Lab, "LabAdmin")]
    [InlineData("SaleUser", SampleRequestWorkAudience.Sale, "Sale")]
    [InlineData("SaleUser", SampleRequestWorkAudience.Sale, "Lab")]
    [InlineData("SaleUser", SampleRequestWorkAudience.Sale, "LabAdmin")]
    [InlineData("President", SampleRequestWorkAudience.Lab, "President")]
    [InlineData("LabAdmin", SampleRequestWorkAudience.LabAdmin, "Sale")]
    [InlineData("LabAdmin", SampleRequestWorkAudience.LabAdmin, "Lab")]
    public void PriorityMineRankMatchesActualWorkAssignment(string role, SampleRequestWorkAudience audience, string owner)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        var employee = user.EmployeeId!.Value;
        var permissions = new CurrentUserPermissionService(user);
        var request = Request("KH_1", "InProgress", null);
        request.ManagerBy = employee;
        var decision = new SampleWorkDecision("stage", "Action", owner);
        var item = new SampleRequestSampleTrialReportDto
        {
            SampleRequestStatus = request.Status, ManagerSalesEmployeeId = employee,
            WorkDecision = decision, CanActForSaleOwner = true
        };
        SampleWorkAssignment.Apply(item, user, permissions, audience);
        var ranked = SampleRequestWorkSortQuery.Enrich(new[] { new SampleRequestSampleTrialReportRow
            { SampleRequest = request, WorkDecision = decision } }.AsQueryable(), audience, new DateTime(2026, 11, 6),
            permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow),
            permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow),
            user.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
            permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule),
            [employee], new[] { request }.AsQueryable()).Single();
        Assert.Equal(item.IsActionForCurrentUser == true ?
            (audience == SampleRequestWorkAudience.LabAdmin ? 0 : 2) : 3, ranked.WorkSortRank);
    }

    [Fact]
    public void PriorityUsesMineThenLabOverdueThenDueNullLastAndClosedLast()
    {
        var rows = new[]
        {
            Row("waiting", "Sale", "InProgress", new DateTime(2026, 11, 1)),
            Row("unknown", "Lab", "InProgress", null),
            Row("later", "Lab", "InProgress", new DateTime(2026, 11, 9)),
            Row("urgent", "Lab", "InProgress", new DateTime(2026, 11, 5)),
            Row("cancelled", "Lab", "Cancelled", new DateTime(2026, 11, 1)),
            Row("done", "Lab", "Completed", new DateTime(2026, 11, 1))
        }.AsQueryable();
        var ranked = SampleRequestWorkSortQuery.Enrich(rows, SampleRequestWorkAudience.Lab, new DateTime(2026, 11, 6),
            true, false, false, false, [], Array.Empty<SampleRequest>().AsQueryable());
        Assert.Equal(new[] { "urgent", "later", "unknown", "waiting", "done", "cancelled" },
            SampleRequestWorkSortQuery.OrderPriority(ranked).Select(x => x.SampleRequest.ExternalId).ToArray());
        Assert.False(ranked.Single(x => x.SampleRequest.ExternalId == "waiting").WorkSortOverdue);
    }

    [Fact]
    public void GroupPriorityUsesOneBestWorkTupleInsteadOfIndependentMinimumDue()
    {
        var ownLater = Row("A", "Lab", "InProgress", new DateTime(2026, 11, 10));
        var waitingEarlier = Row("A", "Sale", "InProgress", new DateTime(2026, 11, 1));
        waitingEarlier.SampleRequest.CustomerId = ownLater.SampleRequest.CustomerId;
        var ownSooner = Row("B", "Lab", "InProgress", new DateTime(2026, 11, 9));
        var ranked = SampleRequestWorkSortQuery.Enrich(new[] { ownLater, waitingEarlier, ownSooner }.AsQueryable(),
            SampleRequestWorkAudience.Lab, new DateTime(2026, 11, 6), true, false, false, false, [],
            Array.Empty<SampleRequest>().AsQueryable());
        var page = new SampleCustomerPriorityPage();
        foreach (var fact in SampleRequestCustomerPageQuery.PriorityFacts(ranked)) page.Include(fact);
        Assert.Equal(ownSooner.SampleRequest.CustomerId, page.GetIds(1, 1).Single());
    }

    [Fact]
    public void AggregateLeaderMatchesLexicographicPriorityForEveryRankAndNullableDue()
    {
        var rows = new List<SampleRequestSampleTrialReportRow>();
        for (var bucket = 0; bucket < 8; bucket++)
        {
            var lead = Row($"KH_{bucket}", "Lab", "InProgress", null);
            lead.WorkSortRank = bucket / 2;
            lead.WorkSortOverdue = bucket % 2 == 0;
            lead.WorkSortDue = bucket % 3 == 0 ? null : new DateTime(2026, 11, 10).AddTicks(bucket);
            rows.Add(lead);
            var alternative = Row($"KH_{bucket}", "Lab", "InProgress", null);
            alternative.SampleRequest.CustomerId = lead.SampleRequest.CustomerId;
            alternative.WorkSortRank = 3;
            alternative.WorkSortOverdue = false;
            alternative.WorkSortDue = new DateTime(2020, 1, 1);
            rows.Add(alternative);
        }
        var expected = rows.GroupBy(x => x.SampleRequest.CustomerId)
            .Select(g => SampleRequestWorkSortQuery.OrderPriority(g.AsQueryable()).First())
            .OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
            .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue)
            .ThenBy(x => x.SampleRequest.Customer.ExternalId).ThenBy(x => x.SampleRequest.CustomerId)
            .Select(x => x.SampleRequest.CustomerId).ToArray();
        var page = new SampleCustomerPriorityPage();
        foreach (var fact in SampleRequestCustomerPageQuery.PriorityFacts(rows.AsQueryable())) page.Include(fact);
        Assert.Equal(expected, page.GetIds(1, 20));
    }

    [Theory]
    [InlineData(SampleRequestWorkAudience.Lab, "Lab", "FinalizeQuotation", "SendSample", "ApproveFormula")]
    [InlineData(SampleRequestWorkAudience.Lab, "Lab", "FinalizeChip", "SendSample", "ReworkFormula")]
    [InlineData(SampleRequestWorkAudience.Sale, "Sale", "CompleteRequest", "FollowUpCustomer", "PriceFeedback")]
    public void ActionPriorityRunsBeforePagingAndPinsStillWin(SampleRequestWorkAudience audience, string role,
        string first, string second, string third)
    {
        var owner = Guid.NewGuid();
        var rows = new[] { Row("other", role, "InProgress", new(2020, 1, 1)),
            Row("feedbackOrFinalize", role, "InProgress", null), Row("sendOrComplete", role, "InProgress", null),
            Row("waiting", role == "Lab" ? "Sale" : "Lab", "InProgress", new(2019, 1, 1)) };
        var actions = new[] { third, second, first, first };
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i].SampleRequest.ManagerBy = owner;
            rows[i].WorkDecision = new("stage", actions[i], rows[i].WorkDecision!.Role);
        }
        var ranked = SampleRequestWorkSortQuery.Enrich(rows.AsQueryable(), audience, new(2026, 11, 11),
            audience == SampleRequestWorkAudience.Lab, audience == SampleRequestWorkAudience.Sale,
            false, false, [owner], rows.Select(x => x.SampleRequest).AsQueryable());
        var ordered = SampleRequestWorkSortQuery.OrderPriority(ranked);
        Assert.Equal(new[] { "sendOrComplete", "feedbackOrFinalize", "other", "waiting" },
            ordered.Select(x => x.SampleRequest.ExternalId));
        Assert.Equal("sendOrComplete", ordered.Take(1).Single().SampleRequest.ExternalId);
        Assert.Equal("feedbackOrFinalize", ordered.Skip(1).Take(1).Single().SampleRequest.ExternalId);
        Guid[] pins = [rows[3].SampleRequest.SampleRequestId];
        Assert.Equal("waiting", SampleRequestWorkSortQuery.OrderPriority(ranked, pins).Take(1).Single().SampleRequest.ExternalId);
        var groups = new SampleCustomerPriorityPage();
        foreach (var fact in SampleRequestCustomerPageQuery.PriorityFacts(ranked, pins)) groups.Include(fact);
        Assert.Equal(rows[3].SampleRequest.CustomerId, groups.GetIds(1, 1).Single());
        Assert.Equal(rows[2].SampleRequest.CustomerId, groups.GetIds(2, 1).Single());
    }

    private static SampleRequestSampleTrialReportRow Row(string code, string role, string status, DateTime? due)
    {
        var request = Request(code, status, due);
        request.ExternalId = code;
        return new() { SampleRequest = request, WorkDecision = new("stage", "Action", role) };
    }

    private static SampleRequest Request(string code, string status, DateTime? due) => new()
    {
        SampleRequestId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Status = status,
        ExpectedDeliveryDate = due, Customer = new Customer { ExternalId = code, CustomerName = code }
    };
}
