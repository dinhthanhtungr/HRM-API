using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseVoucher
    {
        public long VoucherId { get; set; }                 // PK (bigint identity)
        public string VoucherCode { get; set; } = default!; // giống requestCode
        public int VoucherType { get; set; }                // nhập/xuất...
        public int? RequestId { get; set; }                 // FK -> WarehouseRequest.RequestId (int)
        public Guid CompanyId { get; set; }                 // FK -> Companies
        public Guid CreatedBy { get; set; }                 // FK -> Employees
        public DateTime CreatedDate { get; set; }
        public string? Status { get; set; }

        // Navigations (để đơn giản, không yêu cầu collection ở phía Company/Employee/Request)
        public WarehouseRequest? WarehouseRequest { get; set; }
        public Company? Company { get; set; }
        public Employee? CreatedByNavigation { get; set; }

        public ICollection<WarehouseVoucherDetail> Details { get; set; } = new List<WarehouseVoucherDetail>();
    }
}
