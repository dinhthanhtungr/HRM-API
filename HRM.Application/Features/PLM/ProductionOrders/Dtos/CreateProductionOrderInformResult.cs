namespace HRM.Application.Features.PLM.ProductionOrders.Dtos;

public sealed record CreateProductionOrderInformResult(
    Guid MfgProductionOrderId,
    string ExternalId,
    Guid? ManufacturingFormulaId,
    string? ManufacturingFormulaExternalId);
