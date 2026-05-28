using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Attachment
{
    public enum GroupType
    {
        [Display(Name = "Hành chính nhân sự")]
        HCHR,

        [Display(Name = "Quản lý hệ thống")]
        IMS,

        [Display(Name = "Kế toán, tài chính")]
        FinancialAccounting,

        [Display(Name = "Sales Marketing")]
        CMR,

        [Display(Name = "Kho")]
        Warehouse,

        [Display(Name = "Kế hoạch thu mua")]
        PLPU,

        [Display(Name = "Quản lý sản xuất")]
        QLSX,

        [Display(Name = "Quản lý chất lượng")]
        QAQC,

        [Display(Name = "Giao nhận / Logistics")]
        LOGICTIC,

        [Display(Name = "Kỹ thuật & Công nghệ")]
        ENGINEER
    }
}
