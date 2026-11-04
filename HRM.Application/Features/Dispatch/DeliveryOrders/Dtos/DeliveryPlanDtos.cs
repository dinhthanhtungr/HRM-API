using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliveryPlanProgress { NotDelivered, PartiallyDelivered, Delivered }

public sealed record DeliveryPlanResponseDto(DateOnly Date, bool IncludeOverdue, PagedResult<DeliveryPlanItemDto> Plans, bool IncludeAllOrders = false);

/// <summary>Một PO; ngày kế hoạch là ngày hứa muộn nhất của các MFG active có ngày hứa.</summary>
public sealed class DeliveryPlanItemDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string OrderExternalId { get; init; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    /// <summary>Trạng thái PO hiển thị, đồng nhất màn hình đơn hàng; khác tiến độ theo lượng.</summary>
    public string Status { get; init; } = string.Empty;
    public DateOnly? PromisedDeliveryDate { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    /// <summary>Tên sale hiện tại từ assignment active mới nhất, fallback CurrentSaleId; null nếu không hợp lệ.</summary>
    public string? SalesEmployeeFullName { get; init; }
    public string? Receiver { get; init; }
    public string? Phone { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? Note { get; init; }
    public bool IsDeliveryPaused { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal AllocatedQuantity { get; init; }
    public decimal UnallocatedQuantity { get; init; }
    public bool IsOverdue { get; init; }
    public DeliveryPlanProgress Progress { get; init; }
    public IReadOnlyList<DeliveryPlanLineDto> Lines { get; init; } = [];
}

public sealed class DeliveryPlanLineDto
{
    /// <summary>Tổng kg tồn hiện tại của sản phẩm (hàng đạt và hàng lỗi), kệ active cùng company, chưa trừ giữ chỗ.</summary>
    public decimal ImportedQuantity { get; init; }
    /// <summary>Phân rã tổng tồn hiện tại theo loại hàng; cùng scope và đơn vị kg với ImportedQuantity.</summary>
    public DeliveryPlanStockBreakdownDto StockBreakdown { get; init; } = new();
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? Note { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal AllocatedQuantity { get; init; }
    public decimal UnallocatedQuantity { get; init; }
    public DeliveryPlanProgress Progress { get; init; }
    public IReadOnlyList<DeliveryPlanProductionOrderDto> ProductionOrders { get; init; } = [];
    public IReadOnlyList<DeliveryPlanOrderDto> DeliveryOrders { get; init; } = [];
}

public sealed class DeliveryPlanStockBreakdownDto
{
    public decimal FinishedGoodQuantity { get; init; }
    public decimal DefectiveFinishedGoodQuantity { get; init; }
}

public sealed class DeliveryPlanProductionOrderDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? PromisedDeliveryDate { get; init; }
    public decimal? ProductionQuantity { get; init; }
    public IReadOnlyList<string> Lots { get; init; } = [];
}

public sealed class DeliveryPlanOrderDto
{
    public Guid Id { get; init; }
    public string? ExternalId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasPrinted { get; init; }
    public decimal Quantity { get; init; }
    public IReadOnlyList<DeliveryPlanDelivererDto> Deliverers { get; init; } = [];
}

public sealed record DeliveryPlanDelivererDto(Guid Id, string Name);
