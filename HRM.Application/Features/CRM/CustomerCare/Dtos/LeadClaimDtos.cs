namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

public sealed class LeadClaimDetailsDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public IReadOnlyList<LeadClaimOwnerDto> Claims { get; init; } = [];
}

public sealed class LeadClaimOwnerDto
{
    public Guid ClaimId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public Guid GroupId { get; init; }
    public string? GroupName { get; init; }
    public DateTime ExpiresAt { get; init; }
    public double RemainingHours { get; init; }
    public int RemainingDays { get; init; }
    public IReadOnlyList<LeadClaimInteractionDto> Interactions { get; init; } = [];
}

public sealed class LeadClaimInteractionDto
{
    public Guid InteractionId { get; init; }
    public string InteractionType { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? Outcome { get; init; }
    public string? NextAction { get; init; }
    public DateTime InteractionAt { get; init; }
    public DateTime? NextFollowUpDate { get; init; }
}
