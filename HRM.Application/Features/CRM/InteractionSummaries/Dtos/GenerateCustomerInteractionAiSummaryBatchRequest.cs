using HRM.Domain.Enums.CustomerEnum;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.CRM.InteractionSummaries.Dtos;

/// <summary>
/// Điều kiện tạo AI summary đồng nhất cho một danh sách khách hàng.
/// </summary>
public sealed class GenerateCustomerInteractionAiSummaryBatchRequest
{
    public List<Guid> CustomerIds { get; set; } = new();
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionSummaryScope SummaryScope { get; set; } = CustomerInteractionSummaryScope.Monthly;
    public Guid? SaleEmployeeId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool ForceRegenerate { get; set; }
}
