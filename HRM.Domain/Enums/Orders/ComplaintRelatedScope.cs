namespace HRM.Domain.Enums.Orders;

[Flags]
public enum ComplaintRelatedScope
{
    None = 0,
    CustomerClaim = 1 << 0,
    InternalAudit = 1 << 1,
    ProductionControl = 1 << 2
}
