using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class UsagePurpose
    {
        public int PurposeId { get; set; }                  // PK
        public string PurposeCode { get; set; } = default!; // mã logic: STOCK, PICK, HOLD...
        public string PurposeName { get; set; } = default!; // tên hiển thị
        public string? PurposeNote { get; set; }            // ghi chú
        public bool IsActive { get; set; } = true;          // đang dùng?
        public bool IsReceivable { get; set; } = false;     // được nhập về (điểm đến)?
        public bool IsPickable { get; set; } = false;       // được xuất ra (điểm nguồn)?

        public ICollection<WarehouseVoucherDetail> VoucherDetails { get; set; } = new List<WarehouseVoucherDetail>();
    }
}
