using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class TransferDetailMRO
    {
        public long TransferDetailId { get; set; }    // bigint PK
        public long TransferHeaderId { get; set; }    // FK -> header
        public Guid MaterialId { get; set; }    // uuid (FK -> Material) (tuỳ bạn bật/không)
        public int? FromSlotId { get; set; }    // int
        public int? ToSlotId { get; set; }    // int
        public decimal Qty { get; set; }    // numeric
        public string? Note { get; set; }    // text

        public virtual TransferHeaderMRO Header { get; set; } = null!;
    }
}
