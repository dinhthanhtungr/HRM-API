namespace HRM.Domain.Enums.Orders;

[Flags]
public enum ComplaintRelatedStandard
{
    None = 0,
    Quality = 1 << 0,
    GlobalRecycledStandard = 1 << 1,
    HealthAndSafety = 1 << 2,
    Environment = 1 << 3,
    Other = 1 << 4
}
