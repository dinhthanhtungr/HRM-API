using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class EquipmentSpecMRO
    {
        public int SpecId { get; set; }                         // PK (identity)
        public int EquipmentId { get; set; }                    // FK -> equipment
        public string? SpecKey { get; set; }
        public string? SpecValue { get; set; }
        public string? Unit { get; set; }
        public string? Note { get; set; }
        public DateTime? EnteredAt { get; set; }                // timestamp
        public Guid? EnteredBy { get; set; }

        public EquipmentMRO Equipment { get; set; } = null!;
    }
}
