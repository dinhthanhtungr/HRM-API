namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    // Quan sát không tạo participant/read-state và không cấp quyền gửi hay quản lý thành viên.
    internal IQueryable<InternalConversationInboxRow> InboxConversations(bool? archived = null)
    {
        var employee = user.EmployeeId ?? Guid.Empty;
        // Scope đọc đã gồm participant và observer được cấp quyền; không ép membership lần hai.
        var rows = Conversations().Select(conversation => new InternalConversationInboxRow
        {
            InternalConversationId = conversation.InternalConversationId,
            Conversation = conversation,
            // Nhóm rỗng dùng mốc tạo hồ sơ gốc, không biến lúc tạo tab thành lúc gửi tin.
            ActivityAt = conversation.Messages.Where(m => m.InternalConversationId == conversation.InternalConversationId)
                .Max(m => (DateTime?)m.SentAt) ??
                ((conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical ||
                  conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing ||
                  conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom)
                    ? db.InternalConversations.Where(parent => parent.InternalConversationId == conversation.RelatedId &&
                        parent.CompanyId == conversation.CompanyId && parent.IsActive)
                        .Select(parent => (DateTime?)parent.CreatedAt).FirstOrDefault() : null) ?? conversation.CreatedAt,
            IsMember = conversation.Participants.Any(p => p.EmployeeId == employee && p.IsActive),
            IsArchived = conversation.Participants.Where(p => p.EmployeeId == employee && p.IsActive).Select(p => p.IsArchived).FirstOrDefault(),
            IsMuted = conversation.Participants.Where(p => p.EmployeeId == employee && p.IsActive).Select(p => p.IsMuted).FirstOrDefault()
        });
        return archived.HasValue ? rows.Where(row => row.IsArchived == archived.Value) : rows;
    }
}
