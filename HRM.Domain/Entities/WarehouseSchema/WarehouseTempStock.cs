using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseTempStock
    {
        public int TempId { get; set; }                      // PK

        public Guid CompanyId { get; set; }
        // Link tới nghiệp vụ
        public string VaCode { get; set; } = "";              // VD: VA1

        // Vật tư & lô
        public string Code { get; set; } = "";                // mã NVL
        public string? LotKey { get; set; }                   // có thể null (giữ chỗ tổng)

        // Số lượng
        //public decimal? QtyStock { get; set; }                // chỉ dùng cho SNAPSHOT
        public decimal? QtyRequest { get; set; }              // chỉ dùng cho RESERVE
        public decimal? QtyUsed { get; set; }              // chỉ dùng cho RESERVE

        // Trạng thái của RESERVE
        public string? ReserveStatus { get; set; }     // Open/Consumed/Cancelled

        // Audit
        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? UnitName { get; set; }


        public virtual Employee? CreatedByNavigation { get; set; }
    }
}
