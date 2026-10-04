namespace HRM.Application.Abstractions.Notifications;

/// <summary>Archives existing inbox states inside the caller's transaction, after record authorization.</summary>
public interface INotificationInboxArchiver
{
    Task ArchiveConversationAsync(Guid companyId, Guid employeeId, Guid conversationId, CancellationToken cancellationToken);
    Task ArchiveDeletedMessageAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken);
    Task ArchiveGroupAsync(Guid companyId, Guid employeeId, Guid anchorId, CancellationToken cancellationToken);
}
