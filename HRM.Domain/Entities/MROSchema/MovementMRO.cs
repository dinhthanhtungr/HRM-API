using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class MovementMRO
    {
        public Guid MovementId { get; set; }               // uuid (PK)
        public Guid MaterialId { get; set; }               // uuid (không FK theo yêu cầu)
        public int? FromSlotId { get; set; }               // int  (không FK)
        public int? ToSlotId { get; set; }               // int  (không FK)
        public int Qty { get; set; }               // int
        public DateTime MovedAt { get; set; }               // datetime (UTC)
        public string? Note { get; set; }               // text
        public string? RequestExternal { get; set; }          // text
        public string? MoveBy { get; set; }               // text (từ nhiều nguồn)

        public virtual SlotMRO SlotMROFrom { get; set; } = null!;
        public virtual SlotMRO SlotMROTo { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
