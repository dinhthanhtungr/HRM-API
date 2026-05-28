using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Attachment
{
    public enum AttachmentSlot
    {
        Contract,        // Hợp đồng
        PurchaseOrder,   // Đơn đặt hàng
        DeliveryNote,    // Phiếu giao hàng
        Invoice,         // Hóa đơn
        Photo,           // Ảnh hiện trường / sản phẩm
        Specification,   // Spec/tiêu chuẩn kỹ thuật
        Other,           // Khác
        SampleRequest,   // Ảnh mẫu
        ColouredChip,    // Mẫu màu
        QcReport,        // Báo cáo QC
    }

}
