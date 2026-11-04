namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Đồng bộ một notification cá nhân gộp theo tin nhắn và tác giả trong transaction của reaction.</summary>
public interface IInternalMessageReactionNotifications
{
    Task SynchronizeAsync(Guid companyId, Guid conversationId, Guid messageId, Guid actorId, bool isNewActor, CancellationToken ct);
}
