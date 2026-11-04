using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Backgrounds.Notifications;

public sealed class SampleRequestWorkflowNotificationWorker(IServiceProvider services, ILogger<SampleRequestWorkflowNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOneAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Workflow notification processing failed; pending work will be retried."); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessOneAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await db.OutboxMessages.AsNoTracking().Where(x => x.Type == "SampleRequestWorkflowNotification" &&
            x.ProcessedAt == null && x.Attempts < 5).OrderBy(x => x.CreatedAt).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (!id.HasValue) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Khóa hàng trong cùng transaction với message + notification; nhiều worker không phát trùng.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"notification\".\"outbox_messages\" WHERE \"id\"={id.Value} FOR UPDATE", ct);
        var job = await db.OutboxMessages.SingleAsync(x => x.Id == id.Value, ct);
        if (job.ProcessedAt.HasValue || job.Attempts >= 5) { await transaction.RollbackAsync(ct); return; }
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new DispatchSampleRequestWorkflowNotificationCommand(id.Value), ct);
            if (!result.Success)
            {
                var code = Enum.TryParse<WorkflowNotificationFailureCode>(result.Message, out var parsed) && Enum.IsDefined(parsed)
                    ? parsed : WorkflowNotificationFailureCode.Unknown;
                await RecordFailureAsync(code);
                return;
            }
            job.Attempts++;
            job.Error = null;
            job.ProcessedAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await RecordFailureAsync(WorkflowNotificationFailureCode.Unknown);
        }

        async Task RecordFailureAsync(WorkflowNotificationFailureCode code)
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            // Ghi lỗi kỹ thuật chung, không lưu message/note/secret của khách vào error log.
            await db.OutboxMessages.Where(x => x.Id == id.Value && x.ProcessedAt == null && x.Attempts < 5)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.Error, "WorkflowNotification." + code.ToString()), ct);
        }
    }
}
