namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Tín hiệu vô hiệu cache cảm xúc; không tạo Notification hoặc thay đổi badge.</summary>
public interface IInternalMessageReactionEvents
{
    void Enqueue(Guid companyId, Guid conversationId, Guid messageId);
}
