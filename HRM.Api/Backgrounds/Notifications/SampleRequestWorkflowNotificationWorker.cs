using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using HRM.Api.Hubs;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using System.Text.Json;

namespace HRM.Api.Backgrounds.Notifications;

public sealed class SampleRequestWorkflowNotificationWorker(IServiceProvider services, ILogger<SampleRequestWorkflowNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Bounded drain: one slow/error job cannot monopolize the queue.
                for (var i = 0; i < 10 && !stoppingToken.IsCancellationRequested; i++)
                    if (!await ProcessOneAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Workflow notification processing failed; pending work will be retried."); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var job = (await WorkflowNotificationJobQueries.ClaimPending(db.OutboxMessages).ToListAsync(ct)).SingleOrDefault();
        if (job is null) return false;
        try
        {
            if (job.Type == SampleWorkRealtimeSignal.OutboxType)
            {
                // Wake-up only: no request IDs, financial values, body or actor on the company channel.
                using var payload = JsonDocument.Parse(job.PayloadJson);
                var companyId = payload.RootElement.GetProperty("CompanyId").GetGuid();
                if (companyId == Guid.Empty) throw new InvalidOperationException("Invalid sample work signal scope.");
                await scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>()
                    .Clients.Group($"company:{companyId:N}").SendAsync(SampleWorkRealtimeSignal.EventName, new { }, ct);
            }
            else
            {
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new DispatchSampleRequestWorkflowNotificationCommand(job.Id), ct);
                if (!result.Success)
                {
                    var code = Enum.TryParse<WorkflowNotificationFailureCode>(result.Message, out var parsed) && Enum.IsDefined(parsed)
                        ? parsed : WorkflowNotificationFailureCode.Unknown;
                    await RecordFailureAsync(code);
                    return false;
                }
            }
            job.Attempts++;
            job.Error = null;
            job.ProcessedAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            var cause = exception is DbUpdateException update ? update.InnerException ?? exception : exception;
            var code = cause is System.Data.Common.DbException { IsTransient: true }
                ? WorkflowNotificationFailureCode.TransientDatabase
                : cause is JsonException ? WorkflowNotificationFailureCode.InvalidPayload : WorkflowNotificationFailureCode.Unknown;
            logger.LogWarning("Workflow job {JobId} raised {FailureType}", job.Id, cause.GetType().Name);
            await RecordFailureAsync(code);
            return false;
        }

        async Task RecordFailureAsync(WorkflowNotificationFailureCode code)
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            // Ghi lỗi kỹ thuật chung, không lưu message/note/secret của khách vào error log.
            await db.OutboxMessages.Where(x => x.Id == job.Id && x.Type == job.Type && x.ProcessedAt == null && x.Attempts < WorkflowNotificationJobQueries.MaximumAttempts)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.Error, "WorkflowNotification." + code.ToString()), ct);
            logger.LogWarning("Workflow notification job {JobId} of type {JobType} failed with {FailureCode}; attempt {Attempt}/{MaximumAttempts}",
                job.Id, job.Type, code, job.Attempts + 1, WorkflowNotificationJobQueries.MaximumAttempts);
        }
    }
}
