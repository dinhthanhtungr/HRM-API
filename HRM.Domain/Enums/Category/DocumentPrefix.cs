using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Category
{
    public enum DocumentPrefix
    {
        PRQ,    // Phiếu yêu cầu (nhập/xuất kho)
        PXK,    // Phiếu xuất kho
        PNK,    // Phiếu nhập kho
        PCK,    // Phiếu chuyển kho / chuyển kho nội bộ

        KSP,    // Kiểm soát sản xuất (Production Control)

        MFG,    // Lệnh sản xuất
        VA,     // Công thức sản xuất (VA - formula)
        VU,     // Công thức phối màu (Vụ / Masterbatch color formula)

        DHG,    // Đơn hàng (Sales Order)
        PGH,    // Phiếu giao hàng / Delivery note
        DDH,    // Đơn đặt hàng (Purchase Order)

        PM,     // Phiếu bảo trì (maintenance request / maintenance ticket)
        IM,     // Phiếu cải tiến (improvement)
        IR,     // Phiếu sự cố (incident report)
        WO,     // Work Order / Phiếu lệnh sửa chữa
        PR,     // PO mua vật tư
        PO,     // PO mua NVL
        INV,    // Invoice (Hóa đơn)
        RCPT,   //  Biên nhận / Phiếu thu (nếu có)
                // ===== Kế toán: AP (Mua hàng - Công nợ phải trả) =====
        APINV,      // AP Invoice: Hóa đơn mua (purchase.ap_docs doc_kind=INVOICE/BOTH)
        APDN,       // AP Delivery note / Receipt: Phiếu nhận hàng mua (doc_kind=DELIVERY)
        APRET,      // AP Return: Trả hàng NCC (doc_kind=RETURN)
        APADJ,      // AP Adjust: Điều chỉnh công nợ NCC (doc_kind=ADJUST)
        APPAY,      // AP Payment: Phiếu chi NCC (purchase.ap_payments)

        // ===== Kế toán: AR (Bán hàng - Công nợ phải thu) =====
        ARINV,      // AR Invoice: Hóa đơn bán (sales.ar_docs doc_kind=INVOICE/BOTH)
        ARDN,       // AR Delivery: Phiếu giao hàng / xuất bán (doc_kind=DELIVERY)
        ARRET,      // AR Return: Hàng bán bị trả lại (doc_kind=RETURN)
        ARADJ,      // AR Adjust: Điều chỉnh công nợ KH (doc_kind=ADJUST)
        ARRCPT,     // AR Receipt: Phiếu thu khách hàng (sales.ar_receipts)

        // ===== Warehouse Audit / Adjust UI =====
        WAD,    // Warehouse Adjustment (điều chỉnh tồn)
        WIN,    // Warehouse In (thêm/nhập bổ sung)
        WMV,    // Warehouse Move (chuyển kệ)

        // ===== Kế toán: Sổ cái (GL / Journal) =====
        JE,         // Journal Entry: Bút toán tổng hợp (nếu/ khi có bảng acct.gl_journal)
        GLPOST,     // Batch post / Workbench posting batch (nếu cần gom theo đợt)

        // ===== Kế toán: Kỳ kế toán =====
        PER,        // Period (kỳ kế toán)
        CLP,        // Close Period chứng từ khóa kỳ
        OPN,        // Opening (số dư đầu kỳ)
                    // Chý ý
                    //... Riêng phiếu báo cáo sự cố, đề xuất cải tiến và đề xuất vật tư thì prefix là tên code của phòng ban tạo phiếu + "." + enum + "." + mã số gọi từ API)
                    // VD: phiếu báo cáo sự cố thuộc phòng IMS sẽ là: IMS.IR.251200001)
    }
}
