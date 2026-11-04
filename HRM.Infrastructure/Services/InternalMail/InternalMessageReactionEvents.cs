using System.Text.Json;
using HRM.Application.Features.InternalMail.Reactions;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;

namespace HRM.Infrastructure.Services.InternalMail;

/// <summary>Ghi tín hiệu cùng transaction với cảm xúc; không tạo inbox, read-state hoặc Web Push.</summary>
public sealed class InternalMessageReactionEvents(ApplicationDbContext db) : IInternalMessageReactionEvents
{
    public void Enqueue(Guid companyId, Guid conversationId, Guid messageId)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = InternalMessageReactionOutbox.Type,
            PayloadJson = JsonSerializer.Serialize(new InternalMessageReactionEnvelope(companyId, conversationId, messageId))
        });
    }
}
