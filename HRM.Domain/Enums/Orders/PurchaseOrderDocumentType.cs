namespace HRM.Domain.Enums.Orders;
/// <summary>Mã loại chứng từ PO giữ đúng PurchaseFeatures nguồn; không dùng số AttachmentSlot của HRM.</summary>
public enum PurchaseOrderDocumentType
{
    Contract = 0, PurchaseOrder = 1, DeliveryNote = 2, Invoice = 3,
    Photo = 4, Specification = 5, Other = 6, SampleRequest = 7,
    ColouredChip = 8, QcReport = 9, TDS_MSDS = 10, COA = 11, Acceptance = 12
}
