using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingSourceRules
{
    public static readonly string[] EligibleFormulaStatuses =
    [
        FormulaStatus.Approved.ToString(),
        FormulaStatus.SampleSent.ToString(),
        FormulaStatus.Completed.ToString()
    ];

    public static readonly string[] EligibleManufacturingFormulaStatuses =
    [
        ManufacturingProductOrderFormula.IsSelect.ToString(),
        ManufacturingProductOrderFormula.Processing.ToString(),
        ManufacturingProductOrderFormula.Completed.ToString()
    ];

    public const string ReleasedManufacturingVersionStatus = "Released";
}
