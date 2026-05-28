using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyTariff
    {
        public int TariffId { get; set; }    // int PK
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Currency { get; set; } = "VND";
        public string? Utility { get; set; }
        public string? Note { get; set; }
        public virtual ICollection<EnergyGroupTariffMap> EnergyGroupTariffMaps { get; set; } = new List<EnergyGroupTariffMap>();

        public virtual ICollection<EnergyTariffVersion> Versions { get; set; } = new List<EnergyTariffVersion>();
    }
}
