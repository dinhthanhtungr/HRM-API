using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;

namespace HRM.Domain.Entities.WarehouseSchema
{
    public class WarehouseShelves
    {
        public int SlotId { get; set; }                   // PK (identity)
        public string SlotCode { get; set; } = default!;
        public Guid CompanyId { get; set; }

        public decimal CurrentWeightKg { get; set; }      // DECIMAL(10,2)
        public decimal MaxWeightKg { get; set; }          // DECIMAL(10,2)
        public bool IsActive { get; set; } = true;
        public DateTime? LastUpdated { get; set; }

        // Navigations
        public virtual Company Company { get; set; } = default!;
        public virtual ICollection<WarehouseShelfLedger> Ledgers { get; set; } = new List<WarehouseShelfLedger>();
        public virtual ICollection<WarehouseShelfStock> WarehouseShelfStocks { get; set; } = new List<WarehouseShelfStock>();
        public virtual ICollection<WarehouseVoucherDetail> WarehouseVoucherDetails { get; set; } = new List<WarehouseVoucherDetail>();
    }
}
