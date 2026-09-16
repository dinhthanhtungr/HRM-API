namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Kiểm tra quyền đọc conversation ở backend. Participant active luôn đọc được;
/// President/Developer chỉ được bypass participant cho conversation Sample Request cùng company.
/// </summary>
public interface IInternalConversationAccessService
{
    bool CanReadExecutiveSampleRequestConversations { get; }

    Task<bool> CanReadAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
