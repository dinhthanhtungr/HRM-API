using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class RackMRO
    {
        public int RackId { get; set; }
        public int ZoneId { get; set; }
        public string RackName { get; set; } = null!;
        public string RackExternalId { get; set; } = null!;

        public ZoneMRO Zone { get; set; } = null!;
        public ICollection<SlotMRO> Slots { get; set; } = new HashSet<SlotMRO>();
    }
}
