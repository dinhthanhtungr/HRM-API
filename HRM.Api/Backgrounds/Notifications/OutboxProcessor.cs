using System.Text.Json;
using HRM.Api.Hubs;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Backgrounds.Notifications;

/// <summary>
/// Worker nền đọc các dòng notification outbox và phát event SignalR "notify".
/// Trạng thái trong DB vẫn là nguồn chính; realtime chỉ là tín hiệu để FE reload.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await Task.Delay(800, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification outbox processor failed.");
                await Task.Delay(1500, stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
        var areaAccess = scope.ServiceProvider.GetRequiredService<HRM.Application.Features.InternalMail.Services.InternalMailAreaAccessService>();

        // Xử lý message cũ trước để giữ thứ tự notification khi đẩy realtime.
        var messages = await dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAt == null &&
                x.Type == NotificationOutboxTypes.InAppPush)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<OutboxEnvelope>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Notification outbox envelope is invalid.");

                var notification = await dbContext.Notifications.AsNoTracking().FirstOrDefaultAsync(
                    n => n.Id == envelope.NotificationId && n.CompanyId == envelope.CompanyId, cancellationToken);
                if (notification is not null)
                {
                    var queuedRecipients = envelope.TargetUserIds?.ToArray() ?? Array.Empty<Guid>();
                    var recipients = await areaAccess.DeliveryRecipients(notification)
                        .Where(id => queuedRecipients.Contains(id)).ToArrayAsync(cancellationToken);
                    foreach (var employeeId in recipients)
                        await hubContext.Clients.Group($"user:{employeeId}").SendAsync("notify", new { notificationId = notification.Id }, cancellationToken);
                }

                message.Attempts++;
                message.Error = null;
                message.ProcessedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;

                if (message.Attempts >= MaxAttempts)
                {
                    // Tránh một payload lỗi vĩnh viễn làm kẹt worker mãi.
                    message.ProcessedAt = DateTime.Now;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
