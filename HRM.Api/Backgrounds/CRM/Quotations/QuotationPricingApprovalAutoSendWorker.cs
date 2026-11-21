using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRM.Api.Backgrounds.CRM.Quotations;

/// <summary>Retries committed pricing-approval follow-ups independently of the director's HTTP request.</summary>
public sealed class QuotationPricingApprovalAutoSendWorker(
    IServiceScopeFactory scopes, ILogger<QuotationPricingApprovalAutoSendWorker> logger) : BackgroundService
{
    private readonly Dictionary<long, DateTime> _retryAfter = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Quotation auto-send queue unavailable ({ExceptionType}); retrying later.", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var readScope = scopes.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ICRMWriteDbContext>();
        var ids = await readDb.OutboxMessages.AsNoTracking()
            .Where(x => x.Type == QuotationPricingApprovalAutoSend.OutboxType && x.ProcessedAt == null)
            .OrderBy(x => x.Attempts).ThenBy(x => x.Id).Select(x => x.Id).Take(20).ToArrayAsync(cancellationToken);
        foreach (var id in ids)
        {
            if (_retryAfter.TryGetValue(id, out var after) && after > DateTime.UtcNow) continue;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICRMWriteDbContext>();
            var queued = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == id && x.Type == QuotationPricingApprovalAutoSend.OutboxType && x.ProcessedAt == null, cancellationToken);
            if (queued is null) continue;
            string? failure = null;
            try
            {
                var approval = JsonSerializer.Deserialize<QuotationPricingApprovalAutoSend>(queued.PayloadJson)
                    ?? throw new InvalidOperationException("Invalid pricing approval follow-up.");
                await scope.ServiceProvider.GetRequiredService<IQuotationPricingApprovalAutoSendProcessor>()
                    .ProcessAsync(approval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // Do not persist exception messages, prices, reasons or credentials to job diagnostics.
                failure = exception.GetType().Name;
                logger.LogWarning("Quotation auto-send job {JobId} failed ({ExceptionType}); retrying later.", id, failure);
            }
            db.ClearTrackedChanges();
            var current = await db.OutboxMessages.SingleAsync(x => x.Id == id, cancellationToken);
            if (current.ProcessedAt is not null) continue;
            current.Attempts++;
            current.Error = failure;
            if (failure is null)
            {
                current.ProcessedAt = DateTime.Now;
                _retryAfter.Remove(id);
            }
            else _retryAfter[id] = DateTime.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(current.Attempts, 6))));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
