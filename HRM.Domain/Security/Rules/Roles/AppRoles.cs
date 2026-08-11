namespace HRM.Domain.Security.Rules.Roles
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Developer = "Developer";
        public const string SaleUser = "SaleUser";
        public const string MaintenanceUser = "MaintenanceUser";
        public const string ManufactureUser = "ManufactureUser";
        public const string LabUser = "LabUser";
        public const string Leader = "Leader";
        public const string President = "President";
        public const string Purchaser = "Purchaser";
        public const string User = "User";
        public const string IMSUser = "IMSUser";
        public const string DIANUser = "DIANUser";
        public const string KHOUser = "KHOUser";
        public const string HCHRUser = "HCHRUser";
        public const string PLPUUser = "PLPUUser";
        public const string PriceView = "PriceView";
        public const string CustomerViewAll = "CustomerViewAll";
        public const string Delete = "Delete";
        public const string Edit = "Edit";
        public const string LGUser = "LGUser";
        public const string HNUser = "HNUser";
        public const string ACUser = "ACUser";
        public const string DispatchUser = "DispatchUser";

        //role nhận thông báo
        public const string LabNotify = "LabNotify";
        public const string SaleNotify = "SaleNotify";
        public const string PLPUNotify = "PLPUNotify";

        public static readonly string[] All =
        {
            Admin, Developer, SaleUser, MaintenanceUser, ManufactureUser, LabUser, Leader, President,
            Purchaser, User, IMSUser, DIANUser, KHOUser, HCHRUser, PLPUUser, PriceView, CustomerViewAll,
            Delete, Edit, DispatchUser,

            LabNotify, SaleNotify, PLPUNotify
        };
    }
}
