using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class SlotMRO
    {
        public int SlotId { get; set; }
        public int RackId { get; set; } 
        public string SlotName { get; set; } = null!;
        public string SlotExternalId { get; set; } = null!;

        public int CapacityQty { get; set; }
        public bool CountToCapacity { get; set; }

        public RackMRO Rack { get; set; } = null!;
    }
}
