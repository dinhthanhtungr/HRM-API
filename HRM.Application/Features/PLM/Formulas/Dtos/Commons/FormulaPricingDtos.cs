using HRM.Application.Commons.Pricing.Dtos;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons;

public sealed class PatchFormulaPricingRequest
{
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed class FormulaPricingResultDto
{
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public decimal MaterialCost { get; init; }
    public decimal? RealtimeMaterialCost { get; init; }
    public bool IsRealtimeMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }

    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    public FormulaPriceCalculationDto? Pricing { get; init; }
    public DateTime PricingUpdatedDate { get; init; }
    public Guid UpdatedByEmployeeId { get; init; }
}
