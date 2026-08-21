namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class PreviewFormulaPricingPolicyRequest
{
    public decimal MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
}
