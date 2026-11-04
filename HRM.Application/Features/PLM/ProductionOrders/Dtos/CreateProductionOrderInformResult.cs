namespace HRM.Application.Features.PLM.ProductionOrders.Dtos;


/// <summary>
/// Model 
/// </summary>
/// <param name="MfgProductionOrderId"></param>
/// <param name="ExternalId"></param>
/// <param name="ManufacturingFormulaId"></param>
/// <param name="ManufacturingFormulaExternalId"></param>
public sealed record CreateProductionOrderInformResult(
    Guid MfgProductionOrderId,
    string ExternalId,
    Guid? ManufacturingFormulaId,
    string? ManufacturingFormulaExternalId);
