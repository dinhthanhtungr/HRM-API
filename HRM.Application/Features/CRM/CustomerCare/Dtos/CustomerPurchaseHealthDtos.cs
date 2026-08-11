using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos;

/// <summary>
/// Điều kiện lấy khách đã mua, chưa từng mua hoặc đã lâu chưa mua lại.
/// </summary>
public sealed class CustomerPurchaseHealthQuery : PaginationQuery
{
    public Guid? CustomerId { get; init; }
    public Guid? AssignedSaleEmployeeId { get; init; }
    public Guid? GroupId { get; init; }
    public bool OnlyMine { get; init; }
    public CustomerPurchaseStatus PurchaseStatus { get; init; } = CustomerPurchaseStatus.Dormant;
    public int InactivePurchaseDays { get; init; } = 180;
    public DateTime? LastPurchaseFrom { get; init; }
    public DateTime? LastPurchaseTo { get; init; }
}

/// <summary>
/// Báo cáo retention theo lịch sử đơn hàng Merchandise.
/// </summary>
public sealed class CustomerPurchaseHealthReportDto
{
    public CustomerPurchaseHealthHeaderDto Header { get; set; } = default!;
    public PagedResult<CustomerPurchaseHealthRowDto> Customers { get; set; } = default!;
}

public sealed class CustomerPurchaseHealthHeaderDto
{
    public int VisibleCustomerCount { get; set; }
    public int HasPurchasedCount { get; set; }
    public int NeverPurchasedCount { get; set; }
    public int DormantCustomerCount { get; set; }
    public int InactivePurchaseDays { get; set; }
}

public sealed class CustomerPurchaseHealthRowDto
{
    public Guid CustomerId { get; set; }
    public string CustomerExternalId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? AssignedSaleEmployeeId { get; set; }
    public string? AssignedSaleEmployeeName { get; set; }
    public decimal LifetimeOrderAmount { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public int? DaysSinceLastPurchase { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerPurchaseStatus PurchaseStatus { get; set; }
}
