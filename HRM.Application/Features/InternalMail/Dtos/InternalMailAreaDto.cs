namespace HRM.Application.Features.InternalMail.Dtos;

public sealed record InternalMailAreaDto(string Code, Guid? ConversationId, bool CanSend, int UnreadCount, bool CanCreate = false);
