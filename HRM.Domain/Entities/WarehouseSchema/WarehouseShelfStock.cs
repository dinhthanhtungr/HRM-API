using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseShelfStock
    {
        public int ShelfStockId { get; set; }                       // PK (identity/serial)

        public string ShelfStockCode { get; set; } = "";            // kệ/ngăn (bin)
        public int SlotId { get; set; }
        public string Code { get; set; } = "";                // mã NVL
        public string? LotNo { get; set; }
        public string? LotKey { get; set; }                   // số LOT
        public decimal QtyKg { get; set; }                    // khối lượng còn
        public int? Bags { get; set; }

        public StockType StockType { get; set; }               // tình trạng NVL (nguyên/không nguyên)

        public Guid CompanyId { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
        public string UnitName { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }

        // PostgreSQL xmin (row version) — cấu hình trong Fluent API
        //public uint xmin { get; private set; }

        public virtual Employee? UpdatedByNavigation { get; set; }
        public virtual WarehouseShelves? WarehouseShelves { get; set; } = null!;
        public virtual Company? Company { get; set; }
    }
}
