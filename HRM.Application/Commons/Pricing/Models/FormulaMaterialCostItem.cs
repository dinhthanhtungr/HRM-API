using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Models;

public sealed record FormulaMaterialCostItem(
    Guid? ItemId,
    ItemType ItemType,
    decimal Quantity);

public sealed record FormulaRealtimeMaterialCostResult(
    decimal? MaterialCost,
    bool IsComplete,
    int MissingPriceCount);
