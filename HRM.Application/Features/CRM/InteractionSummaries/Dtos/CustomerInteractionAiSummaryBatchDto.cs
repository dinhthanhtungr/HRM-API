namespace HRM.Application.Features.CRM.InteractionSummaries.Dtos;

/// <summary>
/// Kết quả tổng hợp của một batch AI summary.
/// </summary>
public sealed class CustomerInteractionAiSummaryBatchDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public AiRateLimitInfoDto? RateLimit { get; set; }
    public List<CustomerInteractionAiSummaryBatchItemDto> Items { get; set; } = new();
}

/// <summary>
/// Kết quả AI summary của một khách hàng trong batch.
/// </summary>
public sealed class CustomerInteractionAiSummaryBatchItemDto
{
    public Guid CustomerId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CustomerInteractionAiSummaryDto? Summary { get; set; }
}
