using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{

    public class EnergyTariffBandRate
    {
        public int VersionId { get; set; }
        public string Band { get; set; } = "";  // MID/PEAK/OFF…

        public decimal PriceVndPerKwh { get; set; }  // (12,4)

        public virtual EnergyTariffVersion Version { get; set; } = null!;
    }
}
