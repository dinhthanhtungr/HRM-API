namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

public sealed record CustomerTrackingDto(Guid CustomerId, string ExternalId, string CustomerName,
    bool IsLead, string? ResponsibleEmployeeName);
