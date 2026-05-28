using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyReadingsHourlyVn
    {
        public long MeterId { get; set; }       // FK -> energy.meters
        public DateTime TsHourVn { get; set; }       // UTC, đã chuẩn hoá về đầu giờ VN
        public decimal KwhImport { get; set; }       // numeric(14,5)
        public string? Quality { get; set; }
        public string? Source { get; set; }

        public virtual EnergyMeter? Meter { get; set; }
    }
}
