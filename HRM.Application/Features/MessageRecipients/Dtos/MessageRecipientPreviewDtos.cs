namespace HRM.Application.Features.MessageRecipients.Dtos;

public sealed class MessageRecipientPreviewDto
{
    public string ContextType { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public IReadOnlyList<MessageRecipientDto> RequiredRecipients { get; set; } = Array.Empty<MessageRecipientDto>();
    public IReadOnlyList<MessageRecipientDto> SuggestedRecipients { get; set; } = Array.Empty<MessageRecipientDto>();
    public IReadOnlyList<MessageRecipientDto> SelectedRecipients { get; set; } = Array.Empty<MessageRecipientDto>();
    public IReadOnlyList<MessageRecipientDto> SelectedSilentWatchers { get; set; } = Array.Empty<MessageRecipientDto>();
    public bool CanAddRecipients { get; set; } = true;
    public bool CanAddSilentWatchers { get; set; } = true;
    public bool CanRemoveSuggestedRecipients { get; set; } = true;
}

public sealed class MessageRecipientDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool Locked { get; set; }
}
