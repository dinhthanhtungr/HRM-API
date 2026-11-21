using System.Text.Json;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed record InternalMailMediaEnvelope(Guid CompanyId, Guid ConversationId, Guid MessageId, Guid AttachmentId);

public sealed class InternalMailMediaCompletionSignal(IInternalMailDbContext mail, INotificationDbContext notifications)
    : IVideoMediaCompletionSignal
{
    public const string OutboxType = "InternalMailMediaReady";
    public const string EventName = "internalMailMediaReady";

    public async Task PublishAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var items = await mail.InternalMessageAttachments.AsNoTracking().Where(a => a.AttachmentId == attachmentId &&
                a.Attachment.IsActive && !a.Message.IsDeleted && a.Message.Conversation.IsActive)
            .Select(a => new InternalMailMediaEnvelope(a.Message.Conversation.CompanyId,
                a.Message.InternalConversationId, a.InternalMessageId, a.AttachmentId)).ToArrayAsync(cancellationToken);
        foreach (var item in items)
            notifications.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType, PayloadJson = JsonSerializer.Serialize(item), CreatedAt = DateTime.Now
            });
        if (items.Length > 0) await notifications.SaveChangesAsync(cancellationToken);
    }
}
