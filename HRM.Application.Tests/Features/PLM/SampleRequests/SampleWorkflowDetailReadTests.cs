using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkflowDetailReadTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BatchedWorkflowFormulaFieldsKeepTheExistingTrialAndLegacyFallback(bool legacy, bool hasTrial)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var product = Guid.NewGuid();
        Formula Make(string code) => new() { CompanyId = company, ProductId = product, FormulaId = Guid.NewGuid(),
            IsActive = true, ExternalId = code, Status = "Approved", CreatedBy = Guid.NewGuid() };
        var original = Make("VU-original"); var confirmed = Make("VU-confirmed"); var delivered = Make("VU-delivered");
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ProductId = product,
            IsActive = true, FormulaId = original.FormulaId, CreatedDate = new(2026, legacy ? 10 : 11, 2) };
        var trial = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            FormulaId = delivered.FormulaId, IsActive = true, TrialNo = 1 };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Formulas), original, confirmed, delivered);
        var selected = db.SampleRequests.Select(r => new SampleRequestSampleTrialReportRow { SampleRequest = r,
            Trial = hasTrial ? trial : null, ConfirmedFormulaId = confirmed.FormulaId, LabFormulaConfirmedAt = r.CreatedDate });
        var item = new SampleRequestSampleTrialReportDto { SampleRequestId = request.SampleRequestId, ProductId = product,
            SampleRequestSampleTrialId = hasTrial ? trial.SampleRequestSampleTrialId : null,
            SentDate = new DateTime(2026, 11, 3), DeliveredSampleQuantityKg = 2 };
        await SampleRequestDailyPayloadPage.AttachWorkflowFormulasAsync(selected, [item], db, company, default);
        var expected = legacy ? original : hasTrial ? delivered : confirmed;
        Assert.Equal(expected.FormulaId, item.FormulaId);
        Assert.Equal(expected.FormulaId, item.PriceConfirmationFormulaId);
        Assert.Equal(expected.ExternalId, item.FormulaExternalId);
        Assert.Equal(expected.Status, item.FormulaStatus);
        Assert.Equal(expected.CreatedBy, item.FormulaCreatedByEmployeeId);
        Assert.Equal(request.CreatedDate, item.LabFormulaConfirmedAt);
        Assert.Equal(new DateTime(2026, 11, 3), item.SentDate);
        Assert.Equal(2, item.DeliveredSampleQuantityKg);
        Assert.Null(item.WorkDecision);
        Assert.Equal(0, store.Saves);
    }
    [Fact]
    public async Task DeferredHistoryDecisionsKeepEachTrialsFormulaAndOutcome()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var request = new SampleRequest { CompanyId = company, SampleRequestId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(), IsActive = true, CreatedDate = new(2026, 11, 3), Status = "InProgress", SampleQuantity = 2 };
        var oldFormula = new Formula { CompanyId = company, ProductId = request.ProductId, FormulaId = Guid.NewGuid(), IsActive = true, Status = "Approved" };
        var newFormula = new Formula { CompanyId = company, ProductId = request.ProductId, FormulaId = Guid.NewGuid(), IsActive = true, Status = "SampleSent" };
        var old = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            SampleRequest = request, FormulaId = oldFormula.FormulaId, TrialNo = 1, IsActive = true,
            DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.Approved, SentDate = request.CreatedDate };
        var latest = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
            SampleRequest = request, FormulaId = newFormula.FormulaId, TrialNo = 2, IsActive = true,
            DeliveredSampleQuantityKg = 2, Status = SampleTrialStatus.WaitingCustomerFeedback, SentDate = request.CreatedDate.AddDays(1) };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Formulas), oldFormula, newFormula);
        store.Seed(nameof(db.SampleRequestSampleTrials), old, latest);
        store.Seed<SampleRequestWorkflowEvent>(nameof(db.SampleRequestWorkflowEvents));
        var items = new[] { old, latest }.Select(t => new SampleRequestSampleTrialReportDto {
            SampleRequestId = request.SampleRequestId, SampleRequestSampleTrialId = t.SampleRequestSampleTrialId,
            ProductId = request.ProductId, FormulaId = t.FormulaId, SentDate = t.SentDate, Status = t.Status,
            LabFormulaConfirmedAt = request.CreatedDate }).ToArray();
        // Supplying per-trial canonical decisions must not be replaced by the latest trial during workflow attachment.
        items[0].WorkDecision = SampleFlowStateReader.ResolveDecision(request, old, oldFormula, [], false, true, false, null, [old, latest]);
        items[1].WorkDecision = SampleFlowStateReader.ResolveDecision(request, latest, newFormula, [], false, true, true, null, [old, latest]);
        await SampleFlowStateReader.AttachAsync(db, items, company, null, true, false, default);
        Assert.Equal("SendSample", items[0].WorkDecision!.Code);
        Assert.Equal("FollowUpCustomer", items[1].WorkDecision!.Code);
        Assert.Equal(old.FormulaId, items[0].FormulaId);
        Assert.Equal(latest.FormulaId, items[1].FormulaId);
        Assert.Equal(old.SentDate, items[0].SentDate);
        Assert.Equal(SampleTrialStatus.Approved, items[0].Status);
        Assert.Equal(0, store.Saves);
    }
    [Theory]
    [InlineData(SampleRequestWorkSort.WorkPriority)]
    [InlineData(SampleRequestWorkSort.CustomerCode)]
    public void WorkflowPayloadTranslatesWithoutRankingAnAbsentSqlDecision(SampleRequestWorkSort sort)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var id = Guid.NewGuid(); var company = Guid.NewGuid();
        var trials = db.SampleRequestSampleTrials.Where(x => x.IsActive && x.SampleRequestId == id);
        var counts = trials.GroupBy(x => x.SampleRequestId).Select(g => new { Id = g.Key, Count = g.Count(), Last = g.Max(x => x.TrialNo) });
        var latest = from trial in trials join count in counts
                     on new { Id = trial.SampleRequestId, Last = trial.TrialNo } equals new { count.Id, count.Last } select trial;
        var raw = from request in db.SampleRequests.Where(x => x.IsActive && x.CompanyId == company && x.SampleRequestId == id)
                  join count in counts on request.SampleRequestId equals count.Id into countGroup
                  from count in countGroup.DefaultIfEmpty()
                  join trial in latest on request.SampleRequestId equals trial.SampleRequestId into trialGroup
                  from trial in trialGroup.DefaultIfEmpty()
                  select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial, TrialCount = count == null ? null : count.Count };
        var evidence = SampleRequestLabWorkQuery.Enrich(raw, db);
        var detail = SampleRequestWorkSortQuery.Apply(evidence, sort, true, null, DateTime.Today,
            true, true, false, false, [], db.SampleRequests.Where(x => false));
        var sql = detail.Take(1).Select(x => new SampleRequestSampleTrialReportDto {
            SampleRequestId = x.SampleRequest.SampleRequestId, FormulaId = x.ConfirmedFormulaId,
            LabFormulaConfirmedAt = x.LabFormulaConfirmedAt, TrialCount = x.TrialCount ?? 0,
            WorkDecision = null, NeedsLabSchedule = false }).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("ChooseAfterPrice", sql);
        var list = SampleWorkDecisionQuery.Enrich(evidence, db, db, "VND");
        var listSql = SampleRequestWorkSortQuery.Apply(list, sort, false, SampleRequestWorkAudience.Sale,
            DateTime.Today, true, true, false, false, [], db.SampleRequests.Where(x => false))
            .Take(1).Select(x => x.WorkDecision).ToQueryString();
        Assert.Contains("ChooseAfterPrice", listSql);
    }

    [Theory]
    [InlineData("accepted", "ChooseAfterPrice")]
    [InlineData("rejected", "ChooseRejectedPriceAction")]
    [InlineData("lower-price", "AdjustFormulaPrice")]
    [InlineData("pending-kg", "FollowUpCustomer")]
    [InlineData("failed-kg", "ReworkFormula")]
    [InlineData("completed", "")]
    [InlineData("legacy", "SendSample")]
    public async Task SingleRecordWithoutSqlDecisionRetainsCanonicalTaskAndOwnerActions(string scenario, string expectedCode)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid(); var owner = Guid.NewGuid(); var now = new DateTime(2026, 11, 6, 10, 0, 0);
        var request = new SampleRequest { CompanyId = company, SampleRequestId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            ManagerBy = owner, IsActive = true, CreatedDate = scenario == "legacy" ? now.AddMonths(-1) : now,
            RealPriceQuoteDate = now, InfoType = "Quotation", Status = scenario == "completed" ? "Completed" : "InProgress" };
        var formula = new Formula { CompanyId = company, ProductId = request.ProductId, FormulaId = Guid.NewGuid(),
            IsActive = true, Status = scenario == "failed-kg" ? "Rework" : "Approved" };
        request.FormulaId = formula.FormulaId;
        var events = new List<SampleRequestWorkflowEvent> { new() { Id = Guid.NewGuid(), CompanyId = company,
            SampleRequestId = request.SampleRequestId, FormulaId = formula.FormulaId, RecordedAt = now,
            EventType = scenario is "rejected" or "lower-price" ? SampleFlowRules.PriceRejected : SampleRequestWorkflowEvents.PriceAccepted } };
        if (scenario == "lower-price") events.Add(new() { Id = Guid.NewGuid(), CompanyId = company,
            SampleRequestId = request.SampleRequestId, RecordedAt = now.AddSeconds(1), EventType = SampleFlowRules.LowerPriceRequested });
        var trials = new List<SampleRequestSampleTrial>();
        if (scenario is "pending-kg" or "failed-kg")
        {
            request.SampleQuantity = 2;
            trials.Add(new() { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = request.SampleRequestId,
                SampleRequest = request, IsActive = true, FormulaId = formula.FormulaId, TrialNo = 1,
                DeliveredSampleQuantityKg = 2, SentDate = now, Status = scenario == "pending-kg" ? SampleTrialStatus.WaitingCustomerFeedback : SampleTrialStatus.Failed });
        }
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Formulas), formula);
        store.Seed(nameof(db.SampleRequestSampleTrials), trials.ToArray());
        store.Seed(nameof(db.SampleRequestWorkflowEvents), events.ToArray());
        foreach (var authorized in new[] { true, false })
        {
            var item = new SampleRequestSampleTrialReportDto { SampleRequestId = request.SampleRequestId,
                ProductId = request.ProductId, LabFormulaConfirmedAt = now };
            await SampleFlowStateReader.AttachAsync(db, [item], company, owner, true, true, default,
                new Dictionary<Guid, Guid?> { [request.ProductId] = formula.FormulaId },
                authorized ? new HashSet<Guid> { request.SampleRequestId } : new HashSet<Guid>(),
                new Dictionary<Guid, DateTime?> { [request.ProductId] = now.AddSeconds(-1) });
            Assert.Equal(expectedCode, item.WorkDecision!.Code);
            Assert.Equal(item.WorkDecision.Stage, item.Flow!.Stage);
            if (!authorized)
                Assert.DoesNotContain(item.Flow.AllowedActions, x => x is "RequestChip" or "RequestSample" or "RequestLabFinalization" or "RequestLowerPrice");
        }
        Assert.Equal(0, store.Saves);
    }
}
