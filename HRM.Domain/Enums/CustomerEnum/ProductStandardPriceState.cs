namespace HRM.Domain.Enums.CustomerEnum;

/// <summary>
/// Business state of the published product standard price. This is intentionally
/// separate from <see cref="ProductPricingStatus"/>, which is the lifecycle of
/// an individual pricing-version record.
/// </summary>
public enum ProductStandardPriceState
{
    Missing = 0,
    PendingInitialApproval = 10,
    Active = 20,
    PendingReapproval = 30
}
