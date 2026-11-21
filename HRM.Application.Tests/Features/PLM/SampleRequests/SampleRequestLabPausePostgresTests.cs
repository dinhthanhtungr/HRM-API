using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestLabPausePostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task RefusalDeclinesRequestHistoricalPauseCanStillResumeAndConcurrentRefusalsDoNotDuplicate()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Schema(connection, db);
        var company = Guid.NewGuid(); var lab = new Actor(company, Guid.NewGuid(), "LabUser");
        var sale = new Actor(company, Guid.NewGuid(), "SaleUser");
        var date = new DateTime(2026, 11, 2);
        var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, Name = "Test" };
        var product = new Product { ProductId = Guid.NewGuid(), CategoryId = category.CategoryId, CompanyId = company, IsActive = true, Name = "Test" };
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true, CustomerName = "Test" };
        var formula = new Formula { FormulaId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId, IsActive = true,
            Status = "Approved", UpdatedDate = date, ExternalId = "VU_PAUSE", Name = "F001", Note = "Keep this instruction" };
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ProductId = product.ProductId,
            CustomerId = customer.CustomerId, ManagerBy = Guid.NewGuid(), Status = "InProgress", SampleQuantity = 5,
            CreatedDate = date, UpdatedDate = date, IsActive = true, ExternalId = "TP_PAUSE", FormulaId = formula.FormulaId };
        db.Categories.Add(category); db.Products.Add(product); db.Customers.Add(customer); db.Formulas.Add(formula); db.SampleRequests.Add(request);
        db.Employees.AddRange(new Employee { EmployeeId = lab.EmployeeId!.Value, CompanyId = company, IsActive = true },
            new Employee { EmployeeId = sale.EmployeeId!.Value, CompanyId = company, IsActive = true });
        db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(request.SampleRequestId, company, lab.EmployeeId.Value,
            date, SampleRequestWorkflowEvents.FormulaConfirmed, formulaId: formula.FormulaId));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        ApplySampleFlowActionCommandHandler Handler(Actor actor, bool visible = true) => new(db, actor, new CurrentUserPermissionService(actor),
            Visibility.Create(actor, visible), new FormulaVersionService(db, new KeyedMutationLock<Guid>()), null!, db);
        async Task<DateTime?> Token() { db.ChangeTracker.Clear(); return await db.SampleRequests.Select(x => x.UpdatedDate).SingleAsync(); }
        var before = await SampleFlowStateReader.ReadAsync(db, db, await db.SampleRequests.SingleAsync(), true, false, default);
        Assert.False(SampleSaleWorkflowAccess.CanActFor(sale, new CurrentUserPermissionService(sale), request.ManagerBy,
            await Visibility.Create(sale).BuildScopeAsync(default)));
        Assert.False((await Handler(lab).Handle(new() { SampleRequestId = request.SampleRequestId, Action = "RejectFormula", ExpectedUpdatedDate = date }, default)).Success);
        Assert.False((await Handler(sale).Handle(new() { SampleRequestId = request.SampleRequestId, Action = "RejectFormula", ExpectedUpdatedDate = date, Note = "No" }, default)).Success);
        var rejected = await Handler(lab).Handle(new() { SampleRequestId = request.SampleRequestId, Action = "RejectFormula", ExpectedUpdatedDate = date, Note = "  Specification required  " }, default);
        Assert.True(rejected.Success, rejected.Message);
        var token = await Token();
        var parent = await db.SampleRequests.SingleAsync();
        Assert.Equal("LabDeclined", parent.Status);
        Assert.Equal("Approved", await db.Formulas.Select(x => x.Status).SingleAsync());
        Assert.False(await SampleRequestLabPauseRules.IsPausedAsync(db, company, request.SampleRequestId, default));
        var cancellation = await db.SampleRequestWorkflowEvents.SingleAsync(e => e.EventType == SampleRequestWorkflowEvents.LabDeclined);
        Assert.Equal("Specification required", cancellation.Note);
        Assert.Equal(lab.EmployeeId, cancellation.RecordedByEmployeeId);
        Assert.False((await Handler(sale).Handle(new() { SampleRequestId = parent.SampleRequestId,
            Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token, Note = "Cannot resume a cancelled request" }, default)).Success);

        // Historical pause remains readable/resumable. Seed it only in this isolated fixture.
        parent.Status = "InProgress";
        parent.UpdatedDate = DateTime.Now;
        db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, lab.EmployeeId.Value,
            DateTime.Now, SampleRequestLabPauseRules.Rejected, note: "Specification required"));
        await db.SaveChangesAsync();
        token = await Token();
        parent = await db.SampleRequests.SingleAsync();
        Assert.True(await SampleRequestLabPauseRules.IsPausedAsync(db, company, request.SampleRequestId, default));
        var source = from x in db.SampleRequests
                     join t in db.SampleRequestSampleTrials.Where(t => t.IsActive) on x.SampleRequestId equals t.SampleRequestId into historyTrials
                     from t in historyTrials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow { SampleRequest = x, Trial = t, TrialCount = 0,
                         ConfirmedFormulaId = x.FormulaId, LabFormulaConfirmedAt = date,
                         HasPriceFeedback = false, HasAcceptedCustomerPrice = false, HasFinalizedChip = false };
        var sqlRows = SampleWorkDecisionQuery.Enrich(source, db, db, "VND");
        var decision = await sqlRows.Select(x => x.WorkDecision).SingleAsync();
        Assert.Equal("ResumeAfterLabRejection", decision!.Code);
        Assert.Equal("LabRejected", decision.Stage);
        var send = await new SendSampleOptionsCommandHandler(db, db, lab, new CurrentUserPermissionService(lab), Visibility.Create(lab),
            new Clock(), new FormulaVersionService(db, new KeyedMutationLock<Guid>())).Handle(new() {
                SampleRequestId = parent.SampleRequestId, BatchId = Guid.NewGuid(), ExpectedRequestUpdatedDate = token,
                SentDate = date, Options = [new(formula.FormulaId, 2, date)] }, default);
        Assert.False(send.Success); Assert.Contains("paused", send.Message);
        Assert.Empty(await db.SampleRequestSampleTrials.ToListAsync());
        Assert.False((await Handler(lab).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "RejectFormula", ExpectedUpdatedDate = token, Note = "duplicate" }, default)).Success);
        Assert.False((await Handler(lab).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token, Note = "No" }, default)).Success);
        Assert.False((await Handler(sale).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token }, default)).Success);
        Assert.False((await Handler(sale, false).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token, Note = "Hidden" }, default)).Success);
        var otherCompany = new Actor(Guid.NewGuid(), Guid.NewGuid(), "SaleUser");
        Assert.False((await Handler(otherCompany).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token, Note = "Other company" }, default)).Success);
        var resumed = await Handler(sale).Handle(new() { SampleRequestId = parent.SampleRequestId, Action = "ResumeAfterLabRejection", ExpectedUpdatedDate = token, Note = "  Customer supplied specification  " }, default);
        Assert.True(resumed.Success, resumed.Message);
        await Token();
        Assert.False(await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, default));
        var after = await SampleFlowStateReader.ReadAsync(db, db, await db.SampleRequests.SingleAsync(), true, false, default);
        Assert.Equal(before.Stage, after.Stage);
        Assert.Equal(formula.FormulaId, after.FormulaId);
        Assert.Equal("Keep this instruction", await db.Formulas.Select(x => x.Note).SingleAsync());
        var history = await db.SampleRequestWorkflowEvents.Where(e => e.EventType == SampleRequestLabPauseRules.Rejected || e.EventType == SampleRequestLabPauseRules.Resumed).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Specification required", history.Single(e => e.EventType == SampleRequestLabPauseRules.Rejected).Note);
        Assert.Equal(sale.EmployeeId, history.Single(e => e.EventType == SampleRequestLabPauseRules.Resumed).RecordedByEmployeeId);
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        var expected = await Token();
        async Task<bool> ConcurrentReject()
        {
            await using var c = await postgres.OpenAsync();
            await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(c).Options);
            var handler = new ApplySampleFlowActionCommandHandler(context, lab, new CurrentUserPermissionService(lab), Visibility.Create(lab),
                new FormulaVersionService(context, new KeyedMutationLock<Guid>()), null!, context);
            return (await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Action = "RejectFormula", ExpectedUpdatedDate = expected, Note = "Concurrent refusal" }, default)).Success;
        }
        var results = await Task.WhenAll(ConcurrentReject(), ConcurrentReject());
        Assert.Single(results, success => success);
        Assert.Equal(3, await db.OutboxMessages.CountAsync());
        db.ChangeTracker.Clear();
        Assert.Equal("LabDeclined", await db.SampleRequests.Select(x => x.Status).SingleAsync());
        Assert.Equal(2, await db.SampleRequestWorkflowEvents.CountAsync(e => e.EventType == SampleRequestWorkflowEvents.LabDeclined));
        var declinedRequest = await db.SampleRequests.AsNoTracking().SingleAsync();
        var declinedFlow = await SampleFlowStateReader.ReadAsync(db, db, declinedRequest, false, true, default);
        Assert.Equal("LabDeclined", declinedFlow.Stage);
        Assert.Equal("Concurrent refusal", declinedFlow.LabRejectionReason);
        Assert.Empty(declinedFlow.AllowedActions);
        Assert.Contains("RequestSample", declinedFlow.PlanActions);
        var developer = new Actor(company, lab.EmployeeId!.Value, "Developer");
        var reopened = await Handler(developer).Handle(new() { SampleRequestId = request.SampleRequestId,
            Action = "RequestSample", RequestedKg = 3, ExpectedUpdatedDate = declinedRequest.UpdatedDate,
            Note = "Supplemented technical information; request another sample" }, default);
        Assert.True(reopened.Success, reopened.Message);
        db.ChangeTracker.Clear();
        var reopenedRequest = await db.SampleRequests.SingleAsync();
        Assert.Equal("InProgress", reopenedRequest.Status);
        Assert.Equal(request.SampleRequestId, reopenedRequest.SampleRequestId);
        Assert.Equal(product.ProductId, reopenedRequest.ProductId);
        Assert.Equal(2, await db.SampleRequestWorkflowEvents.CountAsync(e => e.EventType == SampleRequestWorkflowEvents.LabDeclined));
        Assert.Equal("Approved", await db.Formulas.Select(x => x.Status).SingleAsync());

    }
    private sealed class Clock : HRM.Application.Abstractions.Commons.Time.IDateTimeProvider { public DateTime Now => DateTime.Now; }
}
