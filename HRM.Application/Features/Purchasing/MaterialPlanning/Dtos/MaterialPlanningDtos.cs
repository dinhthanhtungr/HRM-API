using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;

public sealed record PlanningOrderDto(Guid OrderId, string Code, string PoNumber, string CustomerName, string Status, int LineCount);
public sealed record PlanningLineDto(Guid LineId, Guid ProductId, string Code, string Name,
    decimal RemainingQuantityKg, decimal OnHandQuantityKg, DateTime RequiredDate, IReadOnlyList<PlanningProductionOrderDto> ProductionOrders);
public sealed record PlanningProductionOrderDto(Guid ProductionOrderId, string Code, string Status);
public sealed record PlanningFormulaDto(Guid FormulaId, string Code, string Source, Guid? ProductionOrderId);
public sealed record PlanningNodeDto(Guid ItemId, Guid? SourceLineId, ItemType ItemType, string? Code, string? Name,
    decimal? RequiredQuantityKg, decimal OnHandQuantityKg, bool CanExpand, string? Issue);
public sealed record PlanningExpansionDto(PlanningFormulaDto? Formula, decimal OutputQuantityKg,
    string? Issue, IReadOnlyList<PlanningNodeDto> Items);
