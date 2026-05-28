using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyTariffVersion
    {
        public int VersionId { get; set; }
        public int TariffId { get; set; }
        public DateTime ValidFrom { get; set; }      // lưu type date
        public DateTime? ValidTo { get; set; }      // lưu type date (NULL = còn hiệu lực)

        public decimal VatRate { get; set; }  // (6,4)
        public decimal FuelAdjVndPerKwh { get; set; }  // (12,4)
        public decimal ServiceFixedVndPerMonth { get; set; }  // (14,2)
        public decimal DemandRateVndPerKw { get; set; }  // (14,2)

        public virtual EnergyTariff Tariff { get; set; } = null!;
        public virtual ICollection<EnergyTariffBandRate> BandRates { get; set; } = new List<EnergyTariffBandRate>();
    }
}
