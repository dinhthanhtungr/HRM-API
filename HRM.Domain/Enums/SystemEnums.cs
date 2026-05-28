// File: SharedEnums.cs
// Namespace: Shared.Enums
// NOTE: Bản này “làm sạch/chuẩn hoá format + comment”, KHÔNG đổi tên member, KHÔNG đổi string value,
//       KHÔNG đổi numeric value, KHÔNG reorder enum/member (để tránh phá hệ thống đang chạy).

namespace Shared.Enums
{
    // =========================================================
    // STATUS (string constants)
    // =========================================================

    /// <summary>
    /// Trạng thái đơn sản xuất / luồng sản xuất (string).
    /// Lưu ý: GIỮ NGUYÊN tên + value để không phá hệ thống.
    /// </summary>
    public static class ManufacturingProductOrder
    {
        public const string Unknown = "Unknown";

        // Khi tạo mới đơn hàng
        public const string New = "New";

        // Khi gửi yêu cầu công thức / công thức duyệt
        public const string FormulaRequested = "FormulaRequested";
        public const string FormulaSuccess = "FormulaSuccess";

        // Khi nhấn tạo ct ghi vào SchedulMfg với trạng thái mặc định Scheduling
        public const string Scheduling = "Scheduling";

        // Khi kéo vào máy
        public const string Scheduled = "Scheduled";

        // Chờ NVL
        public const string Waiting = "Waiting";

        // Chỉ ghi log cập nhật SchedulMfg là Scheduling
        public const string Unassign = "Unassign";
        public const string Getback = "Getback";

        // QA/QC ghi vào QCStatus
        public const string QCinprogress = "QCinprogress";
        public const string QCPassed = "QCPassed";
        public const string QCFail = "QCFail";

        // QLSX_CT Status của BTPStatus
        public const string BTPNew = "BTPNew";
        public const string Weighting = "Weighting";
        public const string Weighted = "Weighted";
        public const string Mixing = "Mixing";
        public const string Mixed = "Mixed";

        // QLSX MD
        public const string Started = "Started";
        public const string Canceled = "Canceled";
        public const string Running = "Running";
        public const string Finished = "Finished";

        // Trạng thái phụ chỉ xem được trong Log không cập nhật vào MFG
        // (giữ nguyên value theo hệ thống đang chạy)
        public const string Change = "Valuable";
        public const string Unassignfrommd = "Valuable";

        // Chỉ ghi log và tempendofshift không cập nhật SchedulMfg
        public const string Recored = "Recored";
        public const string Reported = "Reported";

        // Đã tạo yêu cầu nhập kho
        public const string Done = "Done";

        // Đã nhập kho
        public const string Stocked = "Stocked";

        // ===== Aliases an toàn (KHÔNG phá hệ thống) =====
        // Nếu sau này muốn dùng spelling chuẩn hơn trong code mới, dùng các alias này.
        public const string Recorded = Recored; // alias, value vẫn "Recored"
        public const string Valuable = Change;  // alias, value vẫn "Valuable"
    }

    /// <summary>
    /// MerchadiseStatus (string).
    /// </summary>
    public static class MerchadiseStatus
    {
        public const string New = "New";               // (was 0)
        public const string Approved = "Approved";     // (was 1)
        public const string Pending = "Pending";       // (was 2)
        public const string Processing = "Processing"; // (was 3)
        public const string Delivering = "Delivering"; // (was 4)
        public const string Delivered = "Delivered";   // (was 5)
        public const string Paused = "Paused";         // (was 6)
        public const string Cancelled = "Cancelled";   // (was 7)
        public const string Completed = "Completed";   // (was 8)
    }

    /// <summary>
    /// Trạng thái báo cáo cuối ca (để dành mở rộng).
    /// </summary>
    public static class EndOfShiftStatus
    {
        // TODO: bổ sung khi cần. (Giữ nguyên class rỗng để không phá reference hiện có.)
    }

    /// <summary>
    /// Trạng thái phiếu mua vật tư đề xuất (string).
    /// </summary>
    public static class SupplyRequestStatus
    {
        public const string New = "New";                 // Mới
        public const string FirstSuccess = "FirstSuccess";   // Xác nhận lần đầu
        public const string SecondSuccess = "SecondSuccess"; // Xác nhận lần hai
        public const string Buying = "Buying";           // Đang mua
        public const string Puncharted = "Puncharted";   // Đã mua
        public const string Cancelled = "Cancelled";     // Hủy
        public const string Completed = "Completed";     // Hoàn thành
    }

    /// <summary>
    /// Trạng thái giao hàng (string).
    /// </summary>
    public static class DeliveryOrderStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Canceled = "Canceled";
    }

    /// <summary>
    /// Trạng thái đặt hàng (string).
    /// </summary>
    public static class PurchaseOrderStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Canceled = "Canceled";
    }


    /// <summary>
    /// Trạng thái phiếu kho (string).
    /// </summary>
    //public static class WarehouseRequestStatus
    //{
    //    public const string Pending = "Pending";     // (was 0)
    //    public const string Approved = "Approved";   // (was 1)
    //    public const string Cancelled = "Cancelled"; // (was 2)
    //    public const string Completed = "Completed"; // (was 3)
    //}

    /// <summary>
    /// Trạng thái giữ chỗ (string).
    /// </summary>
    public static class ReserveStatus
    {
        public const string Open = "Open";             // (was 1) mở dữ chỗ
        public const string Consumed = "Consumed";     // (was 2) đã hoàn thành
        public const string Cancelled = "Cancelled";   // (was 3) hủy
    }

    /// <summary>
    /// Enum phân loại dòng tạm trong bảng WarehouseTempStock (string).
    /// </summary>
    public enum TempType { Snapshot = 1, Reserve = 2 }

    // =========================================================
    // ENUMS (numeric)
    // =========================================================

    /// <summary>
    /// Cho biết máy móc thiết bị thuộc khu vực nào.
    /// Lưu ý: GIỮ nguyên numeric value.
    /// </summary>
    public enum Area
    {
        None = 0,
        DA = 1, // TP
        TP = 2, // DA
    }

    /// <summary>
    /// Các loại phiếu nhập/xuất kho.
    /// Lưu ý: GIỮ nguyên numeric value.
    /// </summary>
    public enum RequestType // voucherType - header
    {

        ImportFinishedGood = 1,           // Nhập hàng thành phẩm
        ImportDefectiveFinishedGood = 3,  // Nhập hàng thành phẩm lỗi
        ImportRawMaterial = 5,            // Nhập nguyên vật liệu
        ImportDefectiveRawMaterial = 7,   // Nhập nguyên vật liệu lỗi
        ImportOther = 9,                  // Nhập khác

        ExportFinishedGood = 2,           // Xuất hàng thành phẩm
        ExportDefectiveFinishedGood = 4,  // Xuất hàng thành phẩm lỗi
        ExportRawMaterial = 6,            // Xuất nguyên vật liệu
        ExportDefectiveRawMaterial = 8,   // Xuất nguyên vật liệu lỗi
        ExportOther = 10,                 // Xuất khác
    }
    //public enum VoucherDetailType
    //{
    //    Import = 1,
    //    Export = 2,
    //    Transfer = 3,
    //    Adjustment = 4,
    //    Return = 5,
    //    Waiter = 7,
    //    QCPass = 8,
    //    QCFail = 9,
    //    Special = 10,
    //}

    public enum ReqStatus : int
    {
        reqnew = 0,
        Applied = 1,
        Cancelled = 2,
    }

    public enum EventType : int
    {
        ManufacturingProductOrder = 1,
        DeliveryOrderStatus = 2,
        ManufacturingProductOrderFormula = 3,
        MerchadiseStatus = 4,
        SamplerStatus = 5,
        SupplyRequestStatus = 6,
    }

    /// <summary>
    /// Loại tồn kho (đúng enum cậu dùng).
    /// Lưu ý: GIỮ nguyên numeric value.
    /// </summary>
    public enum StockType : int
    {
        FinishedGood = 1,           // Hàng thành phẩm
        DefectiveFinishedGood = 2,  // Hàng thành phẩm lỗi
        RawMaterial = 3,            // Nguyên vật liệu
        DefectiveRawMaterial = 4,   // Nguyên vật liệu lỗi
        Other = 5,                  // Khác


    }

    public enum ManufacturingProductOrderFormula : int
    {
        UnKnow = 0,
        New = 1,            // Mới
        Checking = 2,       // Đang kiểm tra
        IsSelect = 3,       // Duyệt công thức
        Processing = 4,     // Đang xử lý
        Cancelled = 5,      // Hủy
        Error = 6,          // Lỗi
        Completed = 7       // Hoàn thành
    }

    /// <summary>
    /// Công đoạn sản xuất (kế hoạch chọn để biết hàng qua các công đoạn nào).
    /// Lưu ý: GIỮ nguyên numeric value.
    /// </summary>
    //public enum StepOfProduct : int
    //{
    //    Forsb = 0,             // Sang bao
    //    MD_GH = 1,             // Đùn
    //    CT_GH = 2,             // Trộn
    //    HT_GH = 3,             // Trộn Recolor
    //    BN_GH = 4,             // Nghiền
    //    CT_MD_GH = 5,          // Trộn => Đùn
    //    CT_MD_HT_GH = 6,       // Trộn => Đùn => Trộn Recolor
    //    CT_BN_MD_GH = 7,       // Trộn => Nghiền => Đùn
    //    CT_BN_MD_HT_GH = 8,    // Trộn => Nghiền => Đùn => Trộn Recolor
    //}

    /// <summary>
    /// Cho phân quyền người dùng.
    /// Quy tắc: tên phòng ban '.' tổ/bộ phận thuộc phòng ban đó. (VD: QLSX.MD.G1...)
    /// </summary>
    public static class GroupType
    {
        public const string HCHR = "HCHR";             // Hành chính nhân sự
        public const string IMS = "IMS";               // Quản lý hệ thống
        public const string Fin_Acc = "ACC";           // Kế toán, tài chính

        public const string CMR = "CMR";               // Sales Marketing
        public const string CMR_G1 = "CMR.G1";         // Sales Marketing nhóm 1
        public const string CMR_G2 = "CMR.G2";         // Sales Marketing nhóm 2

        public const string WH = "WH";                 // Kho

        public const string PLPU = "PLPU";             // Kế hoạch thu mua - chung
        public const string PLPU_PL = "PLPU.PL";       // Kế hoạch thu mua - lên lệnh sx
        public const string PLPU_PR = "PLPU.PR";       // Kế hoạch thu mua - mua nguyên liệu
        public const string PLPU_LG = "PLPU.LG";       // Kế hoạch thu mua - logistic
        public const string PLPU_MT = "PLPU.MT";       // Kế hoạch thu mua - vật tư

        public const string QLSX = "QLSX";             // Quản lý sx
        public const string QLSX_KTSX = "QLSX.KTSX";   // Quản lý sx
        public const string QLSX_CT = "QLSX.CT";       // Quản lý sx
        public const string QLSX_MD = "QLSX.MD";       // Quản lý sx
        public const string QLSX_HT = "QLSX.HT";       // Quản lý sx
        public const string QLSX_TSBN = "QLSX.TSBN";   // Quản lý sx

        public const string QAQC = "QAQC";             // Quản lý chất lượng
        public const string QAQC_RD = "QAQC.RD";       // Quản lý chất lượng
        public const string QAQC_PM = "QAQC.PM";       // Quản lý chất lượng
        public const string QAQC_LAB = "QAQC.LAB";     // Quản lý chất lượng
        public const string QAQC_QC = "QAQC.QC";       // Quản lý chất lượng

        public const string TECH = "TECH";             // Kĩ thuật và công nghệ
        public const string TECH_IT = "TECH.IT";       // Kĩ thuật và công nghệ
        public const string TECH_EV = "TECH.EV";       // Kĩ thuật và công nghệ
        public const string TECH_MC = "TECH.MC";       // Kĩ thuật và công nghệ
    }

    // =========================================================
    // DOC PREFIX (enum) - dùng ToString() để gọi API sinh mã
    // =========================================================

    /// <summary>
    /// Prefix chứng từ dùng sinh mã (API).
    /// Lưu ý: KHÔNG reorder, KHÔNG đổi tên để tránh phá logic đã chạy.
    /// </summary>
    public enum DocumentPrefix
    {
        PRQ,    // Phiếu yêu cầu (nhập/xuất kho)
        PXK,    // Phiếu xuất kho
        PNK,    // Phiếu nhập kho
        PCK,    // Phiếu chuyển kho / chuyển kho nội bộ

        KSP,

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
        RCPT,   // Biên nhận / Phiếu thu (nếu có)

        // ===== Kế toán: AP (Mua hàng - Công nợ phải trả) =====
        APINV,  // AP Invoice
        APDN,   // AP Delivery note / Receipt
        APRET,  // AP Return
        APADJ,  // AP Adjust
        APPAY,  // AP Payment

        // ===== Kế toán: AR (Bán hàng - Công nợ phải thu) =====
        ARINV,  // AR Invoice
        ARDN,   // AR Delivery
        ARRET,  // AR Return
        ARADJ,  // AR Adjust
        ARRCPT, // AR Receipt

        // ===== Warehouse Audit / Adjust UI =====
        WAD,    // Warehouse Adjustment (điều chỉnh tồn)
        WIN,    // Warehouse In (thêm/nhập bổ sung)
        WMV,    // Warehouse Move (chuyển kệ)

        // ===== Kế toán: Sổ cái (GL / Journal) =====
        JE,     // Journal Entry
        GLPOST, // Batch post / Workbench posting batch

        // ===== Kế toán: Kỳ kế toán =====
        PER,    // Period
        CLP,    // Close Period
        OPN     // Opening (số dư đầu kỳ)

        // Chú ý:
        // ... Riêng phiếu báo cáo sự cố, đề xuất cải tiến và đề xuất vật tư thì prefix = PartCode + "." + enum + "." + mã số gọi từ API
        // VD: IMS.IR.251200001
    }



    //public enum DocumentPrefix
    //{
    //    PRQ,    // Phiếu yêu cầu (nhập/xuất kho)
    //    PXK,    // Phiếu xuất kho
    //    PNK,    // Phiếu nhập kho
    //    PCK,    // Phiếu chuyển kho / chuyển kho nội bộ

    //    KSP,    // Kiểm soát sản xuất (Production Control)

    //    MFG,    // Lệnh sản xuất
    //    VA,     // Công thức sản xuất (VA - formula)
    //    VU,     // Công thức phối màu (Vụ / Masterbatch color formula)

    //    DHG,    // Đơn hàng (Sales Order)
    //    PGH,    // Phiếu giao hàng / Delivery note
    //    DDH,    // Đơn đặt hàng (Purchase Order)

    //    PM,     // Phiếu bảo trì (maintenance request / maintenance ticket)
    //    IM,     // Phiếu cải tiến (improvement)
    //    IR,     // Phiếu sự cố (incident report)
    //    WO,     // Work Order / Phiếu lệnh sửa chữa
    //    PR,     // PO mua vật tư
    //    PO,     // PO mua NVL
    //    INV,    // Invoice (Hóa đơn)
    //    RCPT,   //  Biên nhận / Phiếu thu (nếu có)
    //            // ===== Kế toán: AP (Mua hàng - Công nợ phải trả) =====
    //    APINV,      // AP Invoice: Hóa đơn mua (purchase.ap_docs doc_kind=INVOICE/BOTH)
    //    APDN,       // AP Delivery note / Receipt: Phiếu nhận hàng mua (doc_kind=DELIVERY)
    //    APRET,      // AP Return: Trả hàng NCC (doc_kind=RETURN)
    //    APADJ,      // AP Adjust: Điều chỉnh công nợ NCC (doc_kind=ADJUST)
    //    APPAY,      // AP Payment: Phiếu chi NCC (purchase.ap_payments)

    //    // ===== Kế toán: AR (Bán hàng - Công nợ phải thu) =====
    //    ARINV,      // AR Invoice: Hóa đơn bán (sales.ar_docs doc_kind=INVOICE/BOTH)
    //    ARDN,       // AR Delivery: Phiếu giao hàng / xuất bán (doc_kind=DELIVERY)
    //    ARRET,      // AR Return: Hàng bán bị trả lại (doc_kind=RETURN)
    //    ARADJ,      // AR Adjust: Điều chỉnh công nợ KH (doc_kind=ADJUST)
    //    ARRCPT,     // AR Receipt: Phiếu thu khách hàng (sales.ar_receipts)

    //    // ===== Kế toán: Sổ cái (GL / Journal) =====
    //    JE,         // Journal Entry: Bút toán tổng hợp (nếu/ khi có bảng acct.gl_journal)
    //    GLPOST,     // Batch post / Workbench posting batch (nếu cần gom theo đợt)

    //    // ===== Kế toán: Kỳ kế toán =====
    //    PER,        // Period (kỳ kế toán)
    //    CLP,        // Close Period chứng từ khóa kỳ
    //    OPN,        // Opening (số dư đầu kỳ)
    //                // Chý ý
    //                //... Riêng phiếu báo cáo sự cố, đề xuất cải tiến và đề xuất vật tư thì prefix là tên code của phòng ban tạo phiếu + "." + enum + "." + mã số gọi từ API)
    //                // VD: phiếu báo cáo sự cố thuộc phòng IMS sẽ là: IMS.IR.251200001)
    //}

    // =========================================================
    // SALES / CUSTOMER
    // =========================================================

    public enum CustomerRequestStatus
    {
        New = 0,
        InReview = 1,
        Quoted = 2,
        ClosedWon = 3,
        ClosedLost = 4,
        Cancelled = 9
    }

    public enum SalesQuoteStatus
    {
        Draft = 0,    // nháp
        Sent = 1,     // đã gởi
        Accepted = 2, // đã chấp nhận
        Rejected = 3, // Điều chỉnh
        Expired = 4,  // Quá hạn
        Cancelled = 9 // hủy
    }

    /// <summary>
    /// Trạng thái công thức
    /// </summary>
    public enum FormulaStatus
    {
        Unknown = 0,
        Draft = 1,
        Approved = 2,
        SampleSent = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Enum trạng thái yêu cầu mẫu (sample request).
    /// </summary>
    //public enum SampleRequestStatus
    //{
    //    New,            // Mới
    //    SampleReceived, // Nhận mẫu
    //    InProgress,     // Đang xử lý
    //    SampleSent,     // Gửi mẫu
    //    Cancelled,      // Hủy
    //    Completed        // Hoàn thành
    //}


    /// <summary>
    /// Log hành động liên quan đến công thức sản xuất (Manufacturing Formula).
    /// </summary>
    public enum ManufacturingFormulaLogAction
    {
        Activate = 1,
        Deactivate = 2,
        SetStandard = 3,
        UnsetStandard = 4
    }
}