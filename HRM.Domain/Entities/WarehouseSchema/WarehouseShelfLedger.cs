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
    public class WarehouseShelfLedger
    {
        public long LedgerId { get; set; }                // PK (bigint identity)

        public long? VoucherId { get; set; }              // FK (nếu có bảng phiếu)
        public long? VoucherDetailId { get; set; }        // FK (nếu có)
        public int SlotId { get; set; }                   // FK → WarehouseShelf
        public Guid CompanyId { get; set; }               // FK → Company

        public StockType? StockType { get; set; }

        public string? ProductCode { get; set; }
        public string? LotNumber { get; set; }

        public decimal DeltaKg { get; set; }              // DECIMAL(10,2)
        public decimal BeforeKg { get; set; }             // DECIMAL(10,2)
        public decimal AfterKg { get; set; }              // DECIMAL(10,2)

        public string? Reason { get; set; }

        public Guid? CreatedBy { get; set; }              // FK → Employees
        public DateTime CreatedAt { get; set; }

        public int? PurposeId { get; set; }               // FK (nếu có bảng Purpose)
        public string? RequestCode { get; set; }
        public string? AppSource { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }

        // Navigations (đủ dùng hiện tại)
        public virtual WarehouseShelves Shelf { get; set; } = default!;
        public virtual Company Company { get; set; } = default!;
        public virtual Employee? CreatedByNavigation { get; set; }
    }
}
