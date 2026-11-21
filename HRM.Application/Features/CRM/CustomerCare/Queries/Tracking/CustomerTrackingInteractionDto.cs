namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

public sealed record CustomerTrackingInteractionDto(
    Guid Id, string InteractionType, DateTime InteractionAt, DateTime? NextFollowUpDate);
