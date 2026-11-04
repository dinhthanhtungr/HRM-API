namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Kiểm tra quyền đọc conversation ở backend. Participant active luôn đọc được;
/// Chỉ Developer được bypass membership; luôn giữ company scope. Tên property executive giữ tương thích consumer hiện có.
/// </summary>
public interface IInternalConversationAccessService
{
    bool CanReadExecutiveSampleRequestConversations { get; }

    Task<bool> CanReadAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
