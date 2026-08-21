namespace HRM.Domain.Enums.CustomerEnum;

/// <summary>
/// Cách làm tròn giá được cấu hình theo từng pricing policy.
/// </summary>
public enum FormulaPricingRoundingRule
{
    Nearest = 0,
    Up = 10,
    Down = 20
}
