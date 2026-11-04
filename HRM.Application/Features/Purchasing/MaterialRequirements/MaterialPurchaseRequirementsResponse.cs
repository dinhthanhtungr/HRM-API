using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.Purchasing.MaterialRequirements;

public sealed record MaterialPurchaseRequirementDto(
    Guid MaterialId, string MaterialCode, string MaterialName,
    decimal RequiredQuantityKg, decimal OnHandQuantityKg,
    decimal PendingPurchaseQuantityKg, decimal PurchaseQuantityKg,
    DateTime RequiredBeforeDate, string Status);

public sealed record MaterialPurchaseRequirementsResponse(
    PagedResult<MaterialPurchaseRequirementDto> Rows,
    int ProductionOrdersWithoutReservations)
{
    public bool IsDemandComplete => ProductionOrdersWithoutReservations == 0;
}
