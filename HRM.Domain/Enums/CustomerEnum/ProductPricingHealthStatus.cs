namespace HRM.Domain.Enums.CustomerEnum;

/// <summary>
/// Trạng thái khả dụng của dữ liệu định giá hiện tại, độc lập với việc Formula có hợp lệ hay không.
/// </summary>
public enum ProductPricingHealthStatus
{
    Unknown = 0,
    Ready = 10,
    AwaitingApproval = 20,
    MissingMaterialPrice = 30,
    MaterialCostChanged = 40,
    CostingStale = 50,
    RepricingRequired = 60,
    NoEligibleSource = 70,
    SourceNoLongerEligible = 80,
    PricingPolicyMissing = 90
}
