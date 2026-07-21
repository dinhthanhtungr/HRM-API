using System.Text.Json;
using HRM.Api.Hubs;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Backgrounds;

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

                await PushNotificationAsync(hubContext, envelope, cancellationToken);

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

    private static async Task PushNotificationAsync(
        IHubContext<NotificationHub> hubContext,
        OutboxEnvelope envelope,
        CancellationToken cancellationToken)
    {
        // Giữ realtime payload thật nhỏ; FE dùng id này để gọi API notification có phân quyền.
        var payload = new { notificationId = envelope.NotificationId };
        var companyKey = envelope.CompanyId.ToString("N");

        if (envelope.TargetRoles is { Count: > 0 })
        {
            foreach (var role in envelope.TargetRoles)
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    continue;
                }

                await hubContext.Clients
                    .Group($"role:{companyKey}:{role.Trim().ToUpperInvariant()}")
                    .SendAsync("notify", payload, cancellationToken);
            }
        }

        if (envelope.TargetUserIds is { Count: > 0 })
        {
            foreach (var userId in envelope.TargetUserIds.Where(x => x != Guid.Empty))
            {
                await hubContext.Clients
                    .Group($"user:{userId}")
                    .SendAsync("notify", payload, cancellationToken);
            }
        }
    }
}
