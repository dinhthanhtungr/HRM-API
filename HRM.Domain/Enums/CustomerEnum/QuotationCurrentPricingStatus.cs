namespace HRM.Domain.Enums.CustomerEnum;

public enum QuotationCurrentPricingStatus
{
    Available = 0,
    ProductNotFound = 10,
    FormulaNotFound = 20,
    FormulaMaterialsMissing = 30,
    MaterialPriceMissing = 40,
    ManualTierPriceRequired = 50,
    ApprovedPricingNotFound = 60,
    PricingPolicyMissing = 70
}
