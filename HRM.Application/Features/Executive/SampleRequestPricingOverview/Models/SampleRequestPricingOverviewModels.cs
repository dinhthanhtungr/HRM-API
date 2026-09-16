namespace HRM.Application.Features.Executive.SampleRequestPricingOverview.Models;

internal sealed class SampleRequestOverviewRow
{
    public Guid SampleRequestId { get; init; }
    public Guid ProductId { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ColourCode { get; init; }
    public string? ColourName { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public string? AdditiveCode { get; init; }
    public Guid? LabEmployeeId { get; init; }
    public string? LabName { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid? SaleEmployeeId { get; init; }
    public string? SaleName { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public DateTime? PricingActivityAt { get; init; }
    public DateTime? ConversationActivityAt { get; init; }
    public DateTime LatestActivityAt { get; init; }
}

internal sealed class ConversationOverviewRow
{
    public Guid SampleRequestId { get; init; }
    public Guid ConversationId { get; init; }
    public int TotalMessageCount { get; init; }
    public int UnreadCount { get; init; }
    public DateTime LastMessageAt { get; init; }
    public bool CanOpen { get; init; }
}
