using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.MROSchema
{
    public class AreaMRO
    {
        public int AreaId { get; set; }
        public string AreaExternalId { get; set; } = default!;
        public string AreaName { get; set; } = default!;

        public virtual ICollection<EquipmentMRO> Equipments { get; set; } = new List<EquipmentMRO>();
        //public virtual ICollection<IncidentHeaderMRO> IncidentHeaderMROs { get; set; } = new List<IncidentHeaderMRO>();

    }
}
