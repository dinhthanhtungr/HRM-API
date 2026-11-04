using System.Text.Json;
using HRM.Api.Hubs;
using HRM.Application.Features.InternalMail.Reactions;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Api.Backgrounds.Notifications;

/// <summary>Chỉ gửi ID để client tải lại cảm xúc qua API phân quyền, không phát event notify.</summary>
public sealed class InternalMessageReactionOutboxProcessor(
    IServiceProvider services, ILogger<InternalMessageReactionOutboxProcessor> logger,
    IOptionsMonitor<InternalMailReactionOptions> options) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaximumAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.CurrentValue.Enabled) await ProcessBatchAsync(stoppingToken);
                await Task.Delay(800, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning("Reaction signal processing failed with {FailureType}", exception.GetType().Name);
                await Task.Delay(1500, stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<InternalMailAreaAccessService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
        var messages = await db.OutboxMessages.Where(m => m.ProcessedAt == null &&
                (m.Type == InternalMessageReactionOutbox.Type || m.Type == InternalMessageReactionOutbox.PersonalType))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Take(BatchSize).ToListAsync(ct);
        foreach (var message in messages)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<InternalMessageReactionEnvelope>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Invalid reaction signal.");
                var personal = message.Type == InternalMessageReactionOutbox.PersonalType
                    ? JsonSerializer.Deserialize<InternalMailPersonalActivityEnvelope>(message.PayloadJson)
                        ?? throw new InvalidOperationException("Invalid personal activity signal.") : null;
                if (await db.InternalMessages.AsNoTracking().AnyAsync(m => m.InternalMessageId == envelope.MessageId &&
                    m.InternalConversationId == envelope.ConversationId && m.Conversation.CompanyId == envelope.CompanyId && !m.IsDeleted, ct))
                {
                    // Quyền được xét lại lúc dispatch: người đã bị loại không nhận tín hiệu đang chờ.
                    var query = access.ReactionReaders(envelope.CompanyId, envelope.ConversationId);
                    if (personal is not null) query = query.Where(id => id == personal.RecipientEmployeeId);
                    var readers = await query.ToArrayAsync(ct);
                    if (readers.Length > 0)
                        await hub.Clients.Groups(readers.Select(id => $"user:{id}").ToArray()).SendAsync(
                            personal is null ? InternalMessageReactionOutbox.Event : InternalMessageReactionOutbox.PersonalEvent,
                            new { conversationId = envelope.ConversationId, messageId = envelope.MessageId }, ct);
                }
                message.Error = null;
                message.ProcessedAt = DateTime.Now;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // Không lưu payload/SQL/thông tin người dùng từ exception vào outbox.
                message.Error = exception.GetType().Name;
                if (message.Attempts + 1 >= MaximumAttempts) message.ProcessedAt = DateTime.Now;
            }
            message.Attempts++;
        }
        if (messages.Count > 0) await db.SaveChangesAsync(ct);
    }
}
