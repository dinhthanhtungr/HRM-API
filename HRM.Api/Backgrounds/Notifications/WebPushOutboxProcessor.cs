using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Notifications;
using HRM.Application.Features.Notifications;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Backgrounds.Notifications;

/// <summary>
/// Gui Web Push best-effort tu outbox. Notification DB van la nguon that;
/// worker khong retry toan bo danh sach thiet bi de tranh push trung cho thiet bi da thanh cong.
/// </summary>
public sealed class WebPushOutboxProcessor : BackgroundService
{
    private const int BatchSize = 25;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebPushSender _webPushSender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<WebPushOutboxProcessor> _logger;

    public WebPushOutboxProcessor(
        IServiceProvider serviceProvider,
        IWebPushSender webPushSender,
        IDateTimeProvider dateTimeProvider,
        ILogger<WebPushOutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _webPushSender = webPushSender;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Khong log payload/subscription vi endpoint va key la du lieu nhay cam.
                _logger.LogError(exception, "Web Push outbox processor failed.");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAt == null &&
                x.Type == NotificationOutboxTypes.WebPush)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<WebPushOutboxPayload>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Web Push outbox payload is invalid.");

                var delivery = await DeliverNotificationAsync(
                    dbContext,
                    payload.NotificationId,
                    payload.TargetEmployeeIds,
                    cancellationToken);

                message.Attempts++;
                message.Error = delivery.FailedCount > 0
                    ? $"Web Push failed for {delivery.FailedCount} of {delivery.AttemptedCount} subscriptions."
                    : null;
                message.ProcessedAt = _dateTimeProvider.Now;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Loi ngoai du kien duoc retry toi da nam lan; khong luu exception message co the chua du lieu ngoai.
                message.Attempts++;
                message.Error = "Web Push outbox processing failed.";

                if (message.Attempts >= 5)
                {
                    message.ProcessedAt = _dateTimeProvider.Now;
                }
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<WebPushDeliverySummary> DeliverNotificationAsync(
        ApplicationDbContext dbContext,
        Guid notificationId,
        IReadOnlyCollection<Guid>? targetEmployeeIds,
        CancellationToken cancellationToken)
    {
        if (notificationId == Guid.Empty)
        {
            throw new InvalidOperationException("NotificationId is invalid.");
        }

        var notification = await dbContext.Notifications
            .AsNoTracking()
            .Where(x => x.Id == notificationId)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.Link
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (notification is null)
        {
            return new WebPushDeliverySummary(0, 0);
        }

        var normalizedTargetEmployeeIds = targetEmployeeIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        var subscriptionsQuery = dbContext.WebPushSubscriptions
            .Where(x =>
                x.CompanyId == notification.CompanyId &&
                x.IsActive &&
                x.Employee.IsActive &&
                dbContext.NotificationUserStates.Any(state =>
                    state.NotificationId == notification.Id &&
                    state.UserId == x.EmployeeId &&
                    !state.IsArchived));
        if (normalizedTargetEmployeeIds is not null)
        {
            subscriptionsQuery = subscriptionsQuery.Where(x => normalizedTargetEmployeeIds.Contains(x.EmployeeId));
        }

        var subscriptions = await subscriptionsQuery.ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        var failedCount = 0;
        foreach (var subscription in subscriptions)
        {
            var result = await _webPushSender.SendAsync(
                new WebPushSubscriptionData(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                new WebPushPayload(
                    notification.Id,
                    "B\u1ea1n c\u00f3 th\u00f4ng b\u00e1o m\u1edbi",
                    notification.Link ?? "/notifications"),
                cancellationToken);

            switch (result.Status)
            {
                case WebPushSendStatus.Success:
                    subscription.LastSuccessAt = now;
                    subscription.LastFailureAt = null;
                    subscription.FailureCount = 0;
                    break;

                case WebPushSendStatus.SubscriptionExpired:
                    failedCount++;
                    subscription.IsActive = false;
                    subscription.LastFailureAt = now;
                    subscription.FailureCount++;
                    break;

                case WebPushSendStatus.Disabled:
                    failedCount++;
                    break;

                case WebPushSendStatus.TransientFailure:
                case WebPushSendStatus.PermanentFailure:
                    failedCount++;
                    subscription.LastFailureAt = now;
                    subscription.FailureCount++;
                    break;
            }
        }

        return new WebPushDeliverySummary(subscriptions.Count, failedCount);
    }

    private sealed record WebPushDeliverySummary(int AttemptedCount, int FailedCount);
}
