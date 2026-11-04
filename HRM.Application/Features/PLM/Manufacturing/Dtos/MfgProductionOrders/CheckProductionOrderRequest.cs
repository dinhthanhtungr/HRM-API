namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

public sealed record CheckProductionOrderRequest(bool? IsPrintedStock = false);
