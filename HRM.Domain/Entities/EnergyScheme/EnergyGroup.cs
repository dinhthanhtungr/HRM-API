using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyGroup
    {
        public short GroupId { get; set; }      // smallint PK
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";

        public virtual ICollection<EnergyMeter> Meters { get; set; } = new List<EnergyMeter>();
        public virtual ICollection<EnergyGroupTariffMap> EnergyGroupTariffMaps { get; set; } = new List<EnergyGroupTariffMap>();
    }
}
