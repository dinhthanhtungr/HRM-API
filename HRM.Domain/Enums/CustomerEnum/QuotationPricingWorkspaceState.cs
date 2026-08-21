namespace HRM.Domain.Enums.CustomerEnum;

public enum QuotationPricingWorkspaceState
{
    NoEligibleSource = 0,
    WaitingForPricing = 10,
    Draft = 20,
    ApprovedAvailable = 30,
    Applied = 40
}
