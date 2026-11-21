using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ConversationSummaryCaptureTests
{
    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    public async Task SaveCapturesOnlyTargetKeysBeforeDatabaseWork(bool enabled, bool timestampOnly, int count)
    {
        var interceptor = new StopBeforeDatabase();
        await using var db = Create(enabled, interceptor);
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        db.Attach(sample);
        if (timestampOnly) { sample.UpdatedDate = DateTime.Now; db.Entry(sample).Property(x => x.UpdatedDate).IsModified = true; }
        else { sample.SaleComment = "Never copied to job"; db.Entry(sample).Property(x => x.SaleComment).IsModified = true; }
        await Assert.ThrowsAsync<StoppedSave>(() => db.SaveChangesAsync());
        Assert.Equal(count, interceptor.Jobs.Length);
        foreach (var job in interceptor.Jobs) Assert.DoesNotContain("Never copied", job.PayloadJson);
    }

    [Fact]
    public async Task AuditOnlyAtomicWritersAreCapturedAndCoalescedWithTrackedProduct()
    {
        var interceptor = new StopBeforeDatabase();
        await using var db = Create(true, interceptor);
        var company = Guid.NewGuid(); var id = Guid.NewGuid();
        var product = new Product { ProductId = id, CompanyId = company, ColourName = "Old" };
        db.Attach(product); product.ColourName = "New";
        db.AuditLogs.Add(new AuditLog { CompanyId = company, RecordId = id, SchemaName = "SampleRequests", TableName = "Products" });
        db.AuditLogs.Add(new AuditLog { CompanyId = company, RecordId = Guid.NewGuid(), SchemaName = "SampleRequests", TableName = "SampleRequests" });
        db.AuditLogs.Add(new AuditLog { CompanyId = company, RecordId = Guid.NewGuid(), SchemaName = "Other", TableName = "SampleRequests" });
        await Assert.ThrowsAsync<StoppedSave>(() => db.SaveChangesAsync());
        Assert.Equal(2, interceptor.Jobs.Length);
        Assert.Contains(interceptor.Jobs, x => x.PayloadJson.Contains("Product"));
        Assert.Contains(interceptor.Jobs, x => x.PayloadJson.Contains("Request"));
    }

    private static ApplicationDbContext Create(bool enabled, StopBeforeDatabase interceptor) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1")
            .AddInterceptors(interceptor).Options, Options.Create(new ConversationSummaryOptions { Enabled = enabled }));
    private sealed class StoppedSave : Exception;
    private sealed class StopBeforeDatabase : SaveChangesInterceptor
    {
        public OutboxMessage[] Jobs = [];
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Jobs = eventData.Context!.ChangeTracker.Entries<OutboxMessage>().Select(x => x.Entity).ToArray();
            return ValueTask.FromException<InterceptionResult<int>>(new StoppedSave());
        }
    }
}
