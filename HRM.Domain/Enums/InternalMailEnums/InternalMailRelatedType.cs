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
    // GROUP-STORAGE-TRANSITION: legacy-enum-values — 17/18/19 are persisted legacy child-group discriminators.
    // New structure uses InternalConversationGroup.GroupType + RootConversationId; keep these values during compatibility.
    // Never renumber/reuse them or cast them directly to InternalConversationGroupType (Technical there is 1, not 17).
    // Retire readers/writers only after full cutover and review of stored data, payloads and historical consumers.
    // RelatedId on these child threads points to the parent conversation, not a business record.
    ConversationTechnical = 17,
    ConversationPricing = 18,
    ConversationCustom = 19,
    // Permanent message reference, NOT a legacy group discriminator: retain 20 when removing group compatibility.
    // RelatedId points to the source conversation of a forwarded copy.
    ConversationForwardSource = 20,
    Other = 9999
}
