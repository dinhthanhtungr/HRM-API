using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyRegisterSnapshot
    {
        public long MeterId { get; set; }
        public DateTime TsUtc { get; set; }        // PK cùng meter_id (UTC)

        public decimal KwhTotal { get; set; }       // tổng tích lũy tại thời điểm ts
        public string? Source { get; set; }       // ví dụ: “SCADA”

        public virtual EnergyMeter Meter { get; set; } = null!;
    }
}
