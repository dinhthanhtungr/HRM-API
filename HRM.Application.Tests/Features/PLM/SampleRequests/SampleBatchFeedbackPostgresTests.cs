using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleBatchFeedbackPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public Task OneSaveIsAtomicReplaySafeAndOnlyAcceptedFeedbackCompletes() => CheckAsync(postgres, true);

    internal static async Task CheckAsync(IsolatedGroupStoragePostgres postgres, bool accept)
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Schema(connection, db);
        if (!accept)
            foreach (var entity in new[] { typeof(CustomerAssignment), typeof(CustomerClaim) })
                await SampleDeliveryOptionsPostgresTests.CreateTable(connection, db.Model.FindEntityType(entity)!);
        var company = Guid.NewGuid(); var sale = new Actor(company, Guid.NewGuid(), "SaleUser");
        var date = new DateTime(2026, 11, 14, 8, 0, 0);
        var parent = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ManagerBy = sale.EmployeeId!.Value,
            Status = "SampleSent", CreatedDate = date, UpdatedDate = date, SampleQuantity = 5, InfoType = "Thẻ mẫu", IsActive = true,
            CustomerId = Guid.NewGuid(), ProductId = Guid.NewGuid(), ExternalId = "TP_BATCH" };
        db.Customers.Add(new Customer { CustomerId = parent.CustomerId, CompanyId = company, IsActive = true,
            CustomerName = "Test", CurrentSaleId = sale.EmployeeId });
        db.Products.Add(new Product { ProductId = parent.ProductId, CompanyId = company, IsActive = true, Name = "Test" });
        db.Employees.Add(new Employee { EmployeeId = sale.EmployeeId.Value, CompanyId = company, IsActive = true });
        db.SampleRequests.Add(parent);
        var trials = SampleDeliveryOptionsTests.Trials();
        foreach (var trial in trials)
        {
            trial.SampleRequestId = parent.SampleRequestId; trial.CreatedDate = date; trial.UpdatedDate = date;
            db.Formulas.Add(new Formula { FormulaId = trial.FormulaId!.Value, CompanyId = company, ProductId = parent.ProductId,
                ExternalId = "VU_BATCH_" + trial.TrialNo, Name = "F00" + trial.TrialNo, Status = "SampleSent", IsActive = true, UpdatedDate = date });
        }
        db.SampleRequestSampleTrials.AddRange(trials);
        var events = SampleDeliveryOptionsTests.Events(trials);
        foreach (var evt in events) { evt.CompanyId = company; evt.SampleRequestId = parent.SampleRequestId; evt.RecordedAt = date; evt.OccurredAt = date; }
        db.SampleRequestWorkflowEvents.AddRange(events);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var batchId = events.Single(e => e.EventType == SampleDeliveryRules.BatchSent).Id;
        var permissions = new CurrentUserPermissionService(sale); var visibility = Visibility.Create(sale);
        var single = new RecordSampleOptionFeedbackCommandHandler(db, sale, permissions, visibility, new Clock(), db, new(db));
        var handler = new RecordSampleBatchFeedbackCommandHandler(db, sale, permissions, visibility, FeedbackSender.Create(single), new Clock(), new(db));
        RecordSampleOptionFeedbackCommand Row(int index, bool approved, string? reason = null) => new() {
            OperationId = Guid.NewGuid(), TrialId = trials[index].SampleRequestSampleTrialId, Approved = approved,
            ReplyDate = new(2026, 11, 16), Note = "Feedback " + index, CorrectionReason = reason,
            ExpectedRequestUpdatedDate = date, ExpectedTrialUpdatedDate = date };
        var first = Row(0, accept); var second = Row(1, false);
        second.ReplyDate = new(2026, 11, 1); // A late-row validation failure must roll back the successful first row.
        var command = new RecordSampleBatchFeedbackCommand { SampleRequestId = parent.SampleRequestId, BatchId = batchId,
            OperationId = Guid.NewGuid(), ExpectedRequestUpdatedDate = date,
            Options = accept ? [first, second] : [first, second, Row(2, false)] };
        Assert.False((await handler.Handle(command, default)).Success);
        db.ChangeTracker.Clear();
        Assert.All(await db.SampleRequestSampleTrials.ToListAsync(), t => Assert.Null(t.CustomerReplyDate));
        Assert.Equal(0, await db.CustomerInteractions.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        Assert.Equal(date, (await db.SampleRequests.SingleAsync()).UpdatedDate);
        second.ReplyDate = new(2026, 11, 16);
        var saved = await handler.Handle(command, default);
        Assert.True(saved.Success, saved.Message); db.ChangeTracker.Clear();
        var stored = await db.SampleRequestSampleTrials.OrderBy(t => t.TrialNo).ToArrayAsync();
        if (!accept)
        {
            Assert.All(stored, t => Assert.Equal(SampleTrialStatus.Failed, t.Status));
            var failedRequest = await db.SampleRequests.SingleAsync();
            Assert.Equal("InProgress", failedRequest.Status);
            Assert.Equal("Rework", (await db.Formulas.SingleAsync(f => f.FormulaId == trials[2].FormulaId)).Status);
            Assert.False(await db.Formulas.AnyAsync(f => f.IsSelect));
            var retry = new ApplySampleFlowActionCommandHandler(db, sale, permissions, visibility,
                new FormulaVersionService(db, new KeyedMutationLock<Guid>()), null!, db, new Clock());
            var requested = await retry.Handle(new() { SampleRequestId = parent.SampleRequestId,
                Action = "RequestSample", RequestedKg = 5, Note = "Make it darker",
                ExpectedUpdatedDate = failedRequest.UpdatedDate,
                ExpectedFormulaUpdatedDate = (await db.Formulas.SingleAsync(f => f.FormulaId == trials[2].FormulaId)).UpdatedDate }, default);
            Assert.True(requested.Success, requested.Message);
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.SampleRequestSampleTrials.CountAsync());
            Assert.All(await db.SampleRequestSampleTrials.ToArrayAsync(), t => Assert.Equal(SampleTrialStatus.Failed, t.Status));
            failedRequest = await db.SampleRequests.SingleAsync();
            var cancel = new ApplySampleFlowActionCommand { SampleRequestId = parent.SampleRequestId,
                Action = "CancelRejectedRequest", ExpectedUpdatedDate = failedRequest.UpdatedDate };
            Assert.False((await retry.Handle(cancel, default)).Success);
            db.ChangeTracker.Clear();
            Assert.NotEqual("Cancelled", (await db.SampleRequests.SingleAsync()).Status);
            cancel.Note = "Customer stopped testing";
            var cancelled = await retry.Handle(cancel, default);
            Assert.True(cancelled.Success, cancelled.Message);
            db.ChangeTracker.Clear();
            Assert.Equal("Cancelled", (await db.SampleRequests.SingleAsync()).Status);
            return;
        }
        Assert.Equal(SampleTrialStatus.Approved, stored[0].Status);
        Assert.All(stored.Skip(1), t => Assert.Equal(SampleTrialStatus.Failed, t.Status));
        Assert.Equal(second.Note, stored[1].CustomerReplyNote);
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
        second.Note = "Conflicting retry";
        Assert.False((await handler.Handle(command, default)).Success);
        db.ChangeTracker.Clear();
        var token = (await db.SampleRequests.SingleAsync()).UpdatedDate;
        var correction1 = Row(1, true); var correction2 = Row(2, true, "Customer also accepted this VU");
        foreach (var input in new[] { correction1, correction2 })
        {
            input.ExpectedRequestUpdatedDate = token;
            input.ExpectedTrialUpdatedDate = stored.Single(t => t.SampleRequestSampleTrialId == input.TrialId).UpdatedDate;
        }
        var correction = new RecordSampleBatchFeedbackCommand { SampleRequestId = parent.SampleRequestId, BatchId = batchId,
            OperationId = Guid.NewGuid(), ExpectedRequestUpdatedDate = token, Options = [correction1, correction2] };
        Assert.False((await handler.Handle(correction, default)).Success);
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.SampleRequestSampleTrials.CountAsync(t => t.Status == SampleTrialStatus.Approved));
        correction1.CorrectionReason = "Customer retested and accepted";
        var corrected = await handler.Handle(correction, default);
        Assert.False(corrected.Success); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.SampleRequestSampleTrials.CountAsync(t => t.Status == SampleTrialStatus.Approved));
        Assert.Equal(trials[0].FormulaId, (await db.SampleRequests.SingleAsync()).FormulaId);
        Assert.Equal("Completed", (await db.SampleRequests.SingleAsync()).Status);
        Assert.Equal(trials[0].FormulaId, (await db.Formulas.SingleAsync(f => f.IsSelect)).FormulaId);
        Assert.Equal("Completed", (await db.Formulas.SingleAsync(f => f.IsSelect)).Status);
        var active = db.SampleRequestSampleTrials.Where(t => t.IsActive);
        var latest = active.Where(t => !active.Any(n => n.SampleRequestId == t.SampleRequestId && n.TrialNo > t.TrialNo));
        Assert.Equal(trials[0].FormulaId, await SampleDeliveryRules.Representatives(latest, active, db.SampleRequestWorkflowEvents).Select(t => t.FormulaId).SingleAsync());
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 16, 10, 0, 0); }
}

public sealed class SampleFailedBatchFeedbackPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public Task AllFailedOptionsKeepRetryAndReasonedCancellationAvailable()
        => SampleBatchFeedbackPostgresTests.CheckAsync(postgres, false);
}
