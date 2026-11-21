namespace HRM.Application.Features.InternalMail.Dtos;

public sealed record InternalConversationRelatedProductDto(
    Guid ProductId, string ProductCode, string ProductName,
    IReadOnlyList<InternalConversationRelatedProductTargetDto> Conversations);

public sealed record InternalConversationRelatedProductTargetDto(
    Guid ConversationId, Guid SampleRequestId, string RequestCode);
