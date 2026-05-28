using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{

    // Số liệu giờ (UTC)
    public class EnergyReadingsHourly
    {
        public long MeterId { get; set; }       // FK -> energy.meters
        public DateTime TsUtc { get; set; }       // UTC (timestamptz)
        public decimal KwhImport { get; set; }       // numeric(14,5)
        public string? Quality { get; set; }       // text
        public string? Source { get; set; }       // text

        public virtual EnergyMeter? Meter { get; set; }
    }
}
