namespace HRM.Domain.Security.Rules.Roles
{

    /// <summary>
    /// Tập hợp chuỗi <b>roles</b> dùng trực tiếp cho
    /// - <c>&lt;AuthorizeView Roles="..." /&gt;</c>
    /// - <c>[Authorize(Roles = "...")]</c>
    /// Quy ước: các role cách nhau bằng dấu phẩy, KHÔNG có khoảng trắng.
    /// Ví dụ:
    ///   <code>&lt;AuthorizeView Roles="@RoleSets.PLPU_Purchase"&gt;...&lt;/AuthorizeView&gt;</code>
    ///   <code>[Authorize(Roles = RoleSets.Material_Approval_President)]</code>
    /// </summary>
    public static class RoleSets
    {
        /* ===================== NHÓM CHUNG / TIỆN DỤNG ===================== */

        /// <summary>Nhóm có đặc quyền cao nhất trong dev: Admin + Developer.</summary>
        public const string Admins = $"{AppRoles.President},{AppRoles.Developer}, {AppRoles.Admin}";

        /// <summary>Nhóm quản lý: Trưởng phòng (Leader) và Giám đốc (President).</summary>
        public const string Managers = $"{AppRoles.Leader},{AppRoles.President}";

        /// <summary>Nhóm người dùng phổ thông: User + nhóm chỉ xem toàn bộ khách hàng.</summary>
        public const string Users = $"{AppRoles.User},{AppRoles.CustomerViewAll}";

        /// <summary>Những role được phép xem giá: PriceView, Purchaser, SaleUser, Admin, Developer.</summary>
        public const string PriceReaders = $"{AppRoles.PriceView} ,{AppRoles.Purchaser},{AppRoles.Admin},{AppRoles.Developer},{AppRoles.President}";

        /// <summary>Những role có quyền chỉnh sửa (Edit) hoặc có đặc quyền tương đương: Admin, Developer.</summary>
        public const string Editors = $"{AppRoles.Edit},{AppRoles.Admin},{AppRoles.Developer}";

        /// <summary>Những role có quyền xóa (Delete) hoặc có đặc quyền tương đương: Admin, Developer.</summary>
        public const string Deleters = $"{AppRoles.Delete}";


        /* ===================== WAREHOUSE (KHO) ===================== */

        /// <summary>Nhóm KHO độc lập với PLPU.</summary>
        public const string Warehouse =
            $"{AppRoles.Admin},{AppRoles.Developer},{AppRoles.KHOUser}";


        /* ===================== PLPU (Menu & từng mục) ===================== */

        /// <summary>Mở menu PLPU cho các role liên quan sản xuất/kho/mua hàng.</summary>
        public const string PLPU_Group =
            $"{AppRoles.Developer},{AppRoles.PLPUUser},{AppRoles.President}";

        /// <summary>Lệnh sản xuất (Manufacturing) – PLPU + Sản xuất.</summary>
        public const string PLPU_Mfg =
            $"{AppRoles.Developer},{AppRoles.PLPUUser},{AppRoles.ManufactureUser}";

        /// <summary>Giao hàng của PLPU (logistics nội bộ PLPU/SX) – KHÔNG gồm KHO.</summary>
        public const string PLPU_Shipment =
            $"{AppRoles.Developer},{AppRoles.PLPUUser},{AppRoles.ManufactureUser}";

        /// <summary>Mua hàng của PLPU – Purchaser, SaleUser, PLPUUser, Admin, Dev.</summary>
        public const string PLPU_Purchase =
            $"{AppRoles.Developer},{AppRoles.PLPUUser},{AppRoles.Purchaser},{AppRoles.SaleUser}";


        /* ===================== LABS ===================== */

        /// <summary>Mở menu Labs (LabUser + quyền cao: Admin, Developer).</summary>
        public const string Lab_Group =
            $"{AppRoles.Developer},{AppRoles.LabUser},{AppRoles.President}";


        /* ===================== SALES ===================== */
        public const string LG_Group = $"{AppRoles.LGUser}";
        public const string HN_Group = $"{AppRoles.HNUser}";
        public const string AC_Group = $"{AppRoles.ACUser}";

        /* ===================== CÁC MODULE KHÁC ===================== */

        /// <summary>IMS module: Admin, Dev, IMSUser.</summary>
        public const string IMS = $"{AppRoles.Developer},{AppRoles.IMSUser}";

        /// <summary>DIAN module: Admin, Dev, DIANUser.</summary>
        public const string DIAN = $"{AppRoles.Developer},{AppRoles.DIANUser}";

        /// <summary>HR/HCNS: Admin, Dev, HCHRUser.</summary>
        public const string HR = $"{AppRoles.Developer},{AppRoles.HCHRUser},{AppRoles.President}";

        /// <summary>Bảo trì: Admin, Dev, MaintenanceUser.</summary>
        public const string Maintenance = $"{AppRoles.Developer},{AppRoles.MaintenanceUser}";

        /// <summary>Phòng Lab: Admin, Dev, LabUser.</summary>
        public const string Lab = $"{AppRoles.Developer},{AppRoles.LabUser},{AppRoles.President}";

        /// <summary>Sản xuất: Admin, Dev, ManufactureUser.</summary>
        public const string Manufacturing = $"{AppRoles.Developer},{AppRoles.ManufactureUser},{AppRoles.President}";

        /// <summary>Bán hàng: Admin, Dev, SaleUser.</summary>
        public const string Sales = $"{AppRoles.Developer}, {AppRoles.SaleUser},{AppRoles.President}";
        public const string SaleLeaders = $"{AppRoles.Developer}, {AppRoles.President},{AppRoles.Leader}";
        /// <summary>Mua hàng: Admin, Dev, Purchaser.</summary>
        public const string Purchasing = $"{AppRoles.Developer},{AppRoles.Purchaser}";


        /* ===================== MATERIAL APPROVAL (Duyệt vật tư) ===================== */

        /// <summary>Trang “Trưởng phòng duyệt”: Developer + Leader.</summary>
        public const string Material_Approval_DeptHead = $"{AppRoles.Developer},{AppRoles.Leader}";

        /// <summary>Trang “Giám đốc duyệt”: chỉ President.</summary>
        public const string Material_Approval_President = $"{AppRoles.President}";

        /// <summary>Trang “Vật tư cần mua / phê duyệt thanh toán”: Developer + Purchaser.</summary>
        public const string Material_Approval_Payment = $"{AppRoles.Developer},{AppRoles.Purchaser}";
    }
}
