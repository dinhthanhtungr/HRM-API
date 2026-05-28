using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class WarehouseMRO
    {
        public int WarehouseId { get; set; }
        public string WarehouseExternalId { get; set; } = default!;
        public string WarehouseName { get; set; } = default!;
        public ICollection<ZoneMRO> Zones { get; set; } = new HashSet<ZoneMRO>();
    }
}
