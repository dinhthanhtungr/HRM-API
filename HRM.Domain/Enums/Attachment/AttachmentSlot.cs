namespace HRM.Domain.Enums.Attachment;

public enum AttachmentSlot
{
    Contract,
    PurchaseOrder,
    DeliveryNote,
    Invoice,
    Photo,
    Specification,
    Other,
    SampleRequest,
    ColouredChip,
    QcReport,
    InternalMail,
    Complaint,

    // Tài liệu gắn trực tiếp với nguyên vật liệu. Chỉ thêm ở cuối enum để
    // không làm thay đổi giá trị số của các slot đã lưu trong database.
    MaterialTds = 12,
    MaterialMsds = 13,
    MaterialCoa = 14,
    MaterialCertificate = 15,
    MaterialOther = 16
}
