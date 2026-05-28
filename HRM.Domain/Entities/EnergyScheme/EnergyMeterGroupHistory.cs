using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyMeterGroupHistory
    {
        public long MeterId { get; set; }   // FK -> energy.meters.meter_id (bigint)
        public short GroupId { get; set; }   // FK -> energy.groups.group_id (smallint)
        public DateTime ValidFrom { get; set; }   // UTC
        public DateTime? ValidTo { get; set; }   // UTC (NULL = còn hiệu lực)

        public virtual EnergyMeter? Meter { get; set; }
        public virtual EnergyGroup? Group { get; set; }
    }

}
