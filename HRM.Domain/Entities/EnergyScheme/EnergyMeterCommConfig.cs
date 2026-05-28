using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    // Cấu hình truyền thông của công tơ (1–1 theo meter)
    public class EnergyMeterCommConfig
    {
        public long MeterId { get; set; }   // PK + FK
        public string? Protocol { get; set; }   // text
        public string? SerialPort { get; set; }   // text
        public int? BaudRate { get; set; }
        public string? Parity { get; set; }   // text
        public short? DataBits { get; set; }   // smallint
        public short? StopBits { get; set; }   // smallint
        public short? SlaveId { get; set; }   // smallint
        public int? RegKwhAddr { get; set; }
        public short? RegKwhLen { get; set; }   // smallint
        public string? WordOrder { get; set; }   // text
        public decimal? Scale { get; set; }   // numeric(12,6)

        public virtual EnergyMeter? Meter { get; set; }
    }
}
