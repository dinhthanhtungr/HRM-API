namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>A navigation choice only. A null ID is an eligible default group to initialize on click.</summary>
public sealed record InternalMailQuickGroupDto(string Code, Guid? ConversationId, int UnreadCount = 0);
