namespace HRM.Application.Features.InternalMail.Dtos;

public sealed record InternalMessageReactionCountDto(int ReactionType, int Count);
public sealed record InternalMessageReactionSummaryDto(
    IReadOnlyList<InternalMessageReactionCountDto> Items, int? MyReaction)
{
    public static InternalMessageReactionSummaryDto Empty { get; } = new([], null);
}
public sealed record InternalMessageReactionPersonDto(
    Guid EmployeeId, string EmployeeName, int ReactionType, DateTime CreatedAt, DateTime UpdatedAt);
