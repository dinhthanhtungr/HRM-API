using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Authorization
{
    public static class ApplicationRoles
    {
        public const string Admin = "Admin";
        public const string SysAdmin = "SysAdmin";
        public const string President = "President";
        public const string Developer = "Developer";
        public const string Leader = "Leader";
        public const string User = "User";

        public static class Sales
        {
            public const string SaleUser = "SaleUser";
            public const string CustomerViewAll = "CustomerViewAll";
            public const string PriceView = "PriceView";
        }

        public static class Dispatch
        {
            public const string DispatchUser = "DispatchUser";
        }

        public static class Lab
        {
            public const string LabUser = "LabUser";
        }

        public static class Quality
        {
            public const string QCUser = "QCUser";
        }

        public static class Production
        {
            public const string ManufactureUser = "ManufactureUser";
            public const string QLSXUser = "QLSXUser";
            public const string PLPUUser = "PLPUUser";
        }

        public static class Warehouse
        {
            public const string KHOUser = "KHOUser";
        }

        public static class Purchasing
        {
            public const string Purchaser = "Purchaser";
        }

        public static class Maintenance
        {
            public const string MaintenanceUser = "MaintenanceUser";
        }

        public static class Accounting
        {
            public const string ACCUser = "ACCUser";
            public const string ACUser = "ACUser";
            public const string LGUser = "LGUser";
            public const string HNUser = "HNUser";
        }

        public static class HumanResources
        {
            public const string HC_HRUser = "HC_HRUser";
            public const string HCHRUser = "HCHRUser";
        }

        public static class InternalSystems
        {
            public const string IMSUser = "IMSUser";
            public const string DIANUser = "DIANUser";
        }

        public static class SeePrice
        {
            public const string SeePriceUser = "SeePriceUser";
        }

        public static class Actions
        {
            public const string Edit = "Edit";
            public const string Delete = "Delete";
        }

        public static class Notifications
        {
            public const string LabNotify = "LabNotify";
            public const string SaleNotify = "SaleNotify";
            public const string PLPUNotify = "PLPUNotify";
        }
    }
}
