using System.Text.Json;
using HRM.Api.Hubs;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Backgrounds.Notifications;

public sealed class InternalMailMediaOutboxProcessor(IServiceScopeFactory scopes,
    ILogger<InternalMailMediaOutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var access = scope.ServiceProvider.GetRequiredService<InternalMailAreaAccessService>();
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
                var messages = await db.OutboxMessages.Where(m => m.ProcessedAt == null &&
                    m.Type == InternalMailMediaCompletionSignal.OutboxType).OrderBy(m => m.Id).Take(30).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        var item = JsonSerializer.Deserialize<InternalMailMediaEnvelope>(message.PayloadJson)
                            ?? throw new InvalidDataException();
                        if (await db.InternalMessageAttachments.AsNoTracking().AnyAsync(a =>
                            a.AttachmentId == item.AttachmentId && a.InternalMessageId == item.MessageId && a.Attachment.IsActive &&
                            !a.Message.IsDeleted && a.Message.InternalConversationId == item.ConversationId &&
                            a.Message.Conversation.CompanyId == item.CompanyId && a.Message.Conversation.IsActive, stoppingToken))
                        {
                            var readers = await access.RestrictRealtimeRecipients(
                                access.ReactionReaders(item.CompanyId, item.ConversationId), item.CompanyId)
                                .ToArrayAsync(stoppingToken);
                            if (readers.Length > 0)
                                await hub.Clients.Groups(readers.Select(id => $"user:{id}").ToArray())
                                    .SendAsync(InternalMailMediaCompletionSignal.EventName, new { attachmentId = item.AttachmentId }, stoppingToken);
                        }
                        message.ProcessedAt = DateTime.Now;
                        message.Error = null;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        message.Error = exception.GetType().Name;
                        if (message.Attempts >= 4) message.ProcessedAt = DateTime.Now;
                    }
                    message.Attempts++;
                }
                if (messages.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("Media signal dispatch failed ({ErrorType}).", exception.GetType().Name); }
            await Task.Delay(2000, stoppingToken);
        }
    }
}
