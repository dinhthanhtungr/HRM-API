namespace HRM.Domain.Enums.InternalMailEnums;

public enum InternalMailRelatedType
{
    Customer = 0,
    CustomerInteraction = 1,
    CustomerFollowUpTask = 2,
    WorkTask = 3,
    WorkPlan = 4,
    SampleRequest = 5,
    Formula = 6,
    ManufacturingFormula = 7,
    MfgProductionOrder = 8,
    MerchandiseOrder = 9,
    DeliveryOrder = 10,
    PurchaseOrder = 11,
    Quotation = 12,
    Material = 13,
    Product = 14,
    Supplier = 15,
    Internal = 16,
    // Child threads: RelatedId points to the existing parent conversation, not a business record.
    ConversationTechnical = 17,
    ConversationPricing = 18,
    Other = 9999
}
