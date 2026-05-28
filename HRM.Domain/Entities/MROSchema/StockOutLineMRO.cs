using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class StockOutLineMRO
    {
        public long LineId { get; set; }                  // bigint (PK)
        public long StockOutId { get; set; }              // bigint (FK -> mro.stock_out_hdr)
        public Guid MaterialId { get; set; }              // uuid  (FK -> materials)
        public string? MaterialExternalId { get; set; }    // text  (snapshot)
        public int Qty { get; set; }                     // int
        public string Uom { get; set; } = string.Empty;    // text (not null)
        public string? SlotCode { get; set; }              // text (no FK)
        public string? Note { get; set; }                  // text

        // Navigations (đổi theo tên class thực tế của bạn)
        public virtual StockOutHeaderMRO StockOut { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
