using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.Purchasing.MaterialRequirements;

public sealed record MaterialRequirementSaleOrderDto(
    Guid SaleOrderId, string SaleOrderCode, Guid SaleOrderDetailId,
    Guid CustomerId, string CustomerCode, string CustomerName);

public sealed record MaterialRequirementDemandDto(
    Guid ProductionOrderId, string ProductionOrderCode, string ProductionOrderStatus,
    Guid? ProductId, string? ProductCode, string? ProductName,
    Guid? CustomerId, string? CustomerCode, string? CustomerName,
    DateTime RequiredBeforeDate, decimal RequestedQuantityKg, decimal UsedQuantityKg,
    decimal RequiredQuantityKg, IReadOnlyList<MaterialRequirementSaleOrderDto> SaleOrders);

public sealed record MaterialRequirementDemandsResponse(
    Guid MaterialId, string MaterialCode, string MaterialName,
    decimal MatchedRequiredQuantityKg, PagedResult<MaterialRequirementDemandDto> Rows);
