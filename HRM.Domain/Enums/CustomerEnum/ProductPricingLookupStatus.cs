namespace HRM.Domain.Enums.CustomerEnum;

public enum ProductPricingLookupStatus
{
    NoEligibleSource = 0,
    WaitingForPricing = 10,
    WaitingForApproval = 20,
    Draft = 30,
    Approved = 40
}
