namespace HRM.Domain.Enums.CustomerEnum;

public enum QuotationPricingAvailability
{
    ApprovedPricingAvailable = 10,
    SystemCalculatedAvailable = 20,
    LatestQuotedPriceOnly = 30,
    DraftFormulaOnly = 40,
    NoFormula = 50,
    PricingPolicyMissing = 60
}
