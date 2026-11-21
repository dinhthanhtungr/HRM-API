using HRM.Application.Abstractions.Commons.Time;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.Notifications;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleExplicitBatchFeedbackPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ExplicitResultsRequireOneNoteAndOneWinnerCompletesTheRequestAtomically()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Schema(connection, db);
        var company = Guid.NewGuid(); var sale = new Actor(company, Guid.NewGuid(), "SaleUser");
        var date = new DateTime(2026, 11, 14, 8, 0, 0);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, ManagerBy = sale.EmployeeId!.Value,
            Status = "SampleSent", CreatedDate = date, UpdatedDate = date, SampleQuantity = 5, InfoType = "Thẻ mẫu", IsActive = true,
            CustomerId = Guid.NewGuid(), ProductId = Guid.NewGuid(), ExternalId = "TP_EXPLICIT" };
        db.Customers.Add(new Customer { CustomerId = request.CustomerId, CompanyId = company, IsActive = true });
        db.Products.Add(new Product { ProductId = request.ProductId, CompanyId = company, IsActive = true, Name = "Test" });
        db.Employees.Add(new Employee { EmployeeId = sale.EmployeeId.Value, CompanyId = company, IsActive = true });
        db.SampleRequests.Add(request);
        var trials = SampleDeliveryOptionsTests.Trials();
        foreach (var trial in trials)
        {
            trial.SampleRequestId = request.SampleRequestId; trial.CreatedDate = date; trial.UpdatedDate = date;
            db.Formulas.Add(new Formula { FormulaId = trial.FormulaId!.Value, CompanyId = company, ProductId = request.ProductId,
                ExternalId = "VU_EXPLICIT_" + trial.TrialNo, Name = "F00" + trial.TrialNo, Status = "SampleSent", IsActive = true, UpdatedDate = date });
        }
        db.SampleRequestSampleTrials.AddRange(trials);
        var events = SampleDeliveryOptionsTests.Events(trials);
        foreach (var evt in events) { evt.CompanyId = company; evt.SampleRequestId = request.SampleRequestId; evt.RecordedAt = date; evt.OccurredAt = date; }
        db.SampleRequestWorkflowEvents.AddRange(events);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var batchId = events.Single(e => e.EventType == SampleDeliveryRules.BatchSent).Id;
        var permissions = new CurrentUserPermissionService(sale); var visibility = Visibility.Create(sale);
        var single = new RecordSampleOptionFeedbackCommandHandler(db, sale, permissions, visibility, new Clock(), db, new(db));
        var handler = new RecordSampleBatchFeedbackCommandHandler(db, sale, permissions, visibility, FeedbackSender.Create(single), new Clock(), new(db));
        RecordSampleOptionFeedbackCommand Row(int i, bool approved, DateTime? token, string? note) => new() {
            OperationId = Guid.NewGuid(), TrialId = trials[i].SampleRequestSampleTrialId, Approved = approved,
            ReplyDate = new(2026, 11, 16), Note = note, ExpectedRequestUpdatedDate = token, ExpectedTrialUpdatedDate = date };
        var failed = Row(0, false, date, "  ");
        var first = new RecordSampleBatchFeedbackCommand { SampleRequestId = request.SampleRequestId, BatchId = batchId,
            OperationId = Guid.NewGuid(), ExpectedRequestUpdatedDate = date, PreserveSiblingResults = true, Options = [failed] };
        var missing = await handler.Handle(first, default);
        Assert.False(missing.Success); Assert.Contains("feedback note", missing.Message);
        Assert.Equal(0, await db.CustomerInteractions.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        Assert.All(await db.SampleRequestSampleTrials.AsNoTracking().ToArrayAsync(), t => Assert.Null(t.CustomerReplyDate));
        failed.Note = "Customer rejected option 1";
        Assert.True((await handler.Handle(first, default)).Success); db.ChangeTracker.Clear();
        var stored = await db.SampleRequestSampleTrials.OrderBy(t => t.TrialNo).ToArrayAsync();
        Assert.Equal(SampleTrialStatus.Failed, stored[0].Status);
        Assert.Null(stored[1].CustomerReplyDate); Assert.Null(stored[2].CustomerReplyDate);
        var token = (await db.SampleRequests.SingleAsync()).UpdatedDate;
        var accepted1 = Row(1, true, token, "Customer accepted both options"); var accepted2 = Row(2, true, token, null);
        accepted2.ReplyDate = new(2026, 11, 1);
        var both = new RecordSampleBatchFeedbackCommand { SampleRequestId = request.SampleRequestId, BatchId = batchId,
            OperationId = Guid.NewGuid(), ExpectedRequestUpdatedDate = token, PreserveSiblingResults = true, Options = [accepted1, accepted2] };
        Assert.False((await handler.Handle(both, default)).Success); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.CustomerInteractions.CountAsync());
        Assert.Equal(0, await db.SampleRequestSampleTrials.CountAsync(t => t.Status == SampleTrialStatus.Approved));
        accepted2.ReplyDate = new(2026, 11, 16);
        Assert.False((await handler.Handle(both, default)).Success);
        db.ChangeTracker.Clear();
        both.Options = [accepted1];
        var saved = await handler.Handle(both, default); Assert.True(saved.Success, saved.Message); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.SampleRequestSampleTrials.CountAsync(t => t.Status == SampleTrialStatus.Approved));
        Assert.Equal(2, await db.SampleRequestSampleTrials.CountAsync(t => t.Status == SampleTrialStatus.Failed));
        Assert.Equal("Completed", (await db.SampleRequests.SingleAsync()).Status);
        Assert.Equal(trials[1].FormulaId, (await db.SampleRequests.SingleAsync()).FormulaId);
        Assert.Equal("Customer rejected option 1", (await db.SampleRequestSampleTrials.SingleAsync(t => t.SampleRequestSampleTrialId == trials[0].SampleRequestSampleTrialId)).CustomerReplyNote);
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        var payloads = (await db.OutboxMessages.ToListAsync()).Select(job => JsonSerializer.Deserialize<WorkflowNotificationPayload>(job.PayloadJson)!).ToArray();
        var summary = Assert.Single(payloads, payload => payload.EventId == both.OperationId);
        Assert.Equal(TopicNotifications.SampleRequestCustomerFeedbackRecorded, summary.Topic);
        Assert.Contains("VU_EXPLICIT_2", summary.Message);
        Assert.Contains("Customer accepted both options", summary.Message);
        Assert.DoesNotContain("VU_EXPLICIT_3", summary.Message);
        Assert.True((await handler.Handle(both, default)).Success);
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        both.PreserveSiblingResults = false;
        Assert.False((await handler.Handle(both, default)).Success);
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 16, 10, 0, 0); }
}
