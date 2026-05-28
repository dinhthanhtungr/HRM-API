using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.EnergyScheme
{
    public class EnergyMeter
    {
        public long MeterId { get; set; }   // bigint PK
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Multiplier { get; set; }  // numeric(10,4)
        public bool IsActive { get; set; } = true;

        public short GroupId { get; set; }   // FK -> groups
        public virtual EnergyGroup Group { get; set; } = null!;
    }
}
