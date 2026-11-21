using System.Reflection;
using HRM.Api.Backgrounds.CRM.Quotations;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationAutoSendWorkerTests
{
    [Fact]
    public async Task FailedJobDoesNotStopOthersAndRestartRetriesDurableJob()
    {
        var db = DispatchProxy.Create<ICRMWriteDbContext, WorkerStore>();
        var store = (WorkerStore)(object)db;
        var first = QuotationPricingApprovalAutoSend.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Now);
        first.Id = 1;
        var second = QuotationPricingApprovalAutoSend.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Now);
        second.Id = 2;
        store.Seed("OutboxMessages", first, second);
        var processor = new Processor { FailCompany = System.Text.Json.JsonSerializer.Deserialize<QuotationPricingApprovalAutoSend>(first.PayloadJson)!.CompanyId };
        using var services = new ServiceCollection().AddSingleton(db)
            .AddSingleton<IQuotationPricingApprovalAutoSendProcessor>(processor).BuildServiceProvider();
        var factory = services.GetRequiredService<IServiceScopeFactory>();
        using (var worker = new QuotationPricingApprovalAutoSendWorker(factory, NullLogger<QuotationPricingApprovalAutoSendWorker>.Instance))
            await worker.ProcessBatchAsync(default);
        Assert.Null(first.ProcessedAt);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(nameof(InvalidOperationException), first.Error);
        Assert.NotNull(second.ProcessedAt);
        Assert.Equal(2, processor.Calls);

        processor.FailCompany = null;
        // New worker simulates a restart: the event is still pending in the durable store.
        using (var restarted = new QuotationPricingApprovalAutoSendWorker(factory, NullLogger<QuotationPricingApprovalAutoSendWorker>.Instance))
        {
            await restarted.ProcessBatchAsync(default);
            await restarted.ProcessBatchAsync(default);
        }
        Assert.NotNull(first.ProcessedAt);
        Assert.Null(first.Error);
        Assert.Equal(2, first.Attempts);
        Assert.Equal(3, processor.Calls); // completed jobs are not invoked again
    }

    [Fact]
    public async Task UnrelatedOutboxAndCompletedJobsAreNotProcessed()
    {
        var db = DispatchProxy.Create<ICRMWriteDbContext, WorkerStore>();
        var store = (WorkerStore)(object)db;
        store.Seed("OutboxMessages", new OutboxMessage { Id = 1, Type = "InAppPush", PayloadJson = "{}" },
            new OutboxMessage { Id = 2, Type = QuotationPricingApprovalAutoSend.OutboxType, PayloadJson = "{}", ProcessedAt = DateTime.Now });
        var processor = new Processor();
        using var services = new ServiceCollection().AddSingleton(db)
            .AddSingleton<IQuotationPricingApprovalAutoSendProcessor>(processor).BuildServiceProvider();
        using var worker = new QuotationPricingApprovalAutoSendWorker(services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QuotationPricingApprovalAutoSendWorker>.Instance);
        await worker.ProcessBatchAsync(default);
        Assert.Equal(0, processor.Calls);
    }

    private sealed class Processor : IQuotationPricingApprovalAutoSendProcessor
    {
        public Guid? FailCompany { get; set; }
        public int Calls { get; private set; }
        public Task ProcessAsync(QuotationPricingApprovalAutoSend approval, CancellationToken cancellationToken)
        {
            Calls++;
            return approval.CompanyId == FailCompany
                ? Task.FromException(new InvalidOperationException("Do not persist internal exception details"))
                : Task.CompletedTask;
        }
    }

    public class WorkerStore : IqcTestStore
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method!.Name == "ClearTrackedChanges" ? null : base.Invoke(method, args);
    }
}
