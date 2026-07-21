namespace HRM.Domain.Enums.WorkTaskEnums;

/// <summary>
/// Loai nghiep vu duoc WorkTask/WorkPlan tham chieu mem. Gia tri enum da luu DB nen chi them moi o cuoi.
/// </summary>
public enum WorkReferenceType
{
    Customer = 0,
    CustomerInteraction = 1,
    MerchandiseOrder = 2,
    DeliveryOrder = 3,
    PurchaseOrder = 4,
    Internal = 5,
    Custom = 6,
    Other = 9999
}
