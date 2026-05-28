using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.StandardParameterSchema
{
    public class ParameterStandard
    {
        public string MachineId { get; set; } = string.Empty;      // PK-part
        public string ProductionCode { get; set; } = string.Empty;  // PK-part

        public int Set1Standard { get; set; }
        public int Set2Standard { get; set; }
        public int Set3Standard { get; set; }
        public int Set4Standard { get; set; }
        public int Set5Standard { get; set; }
        public int Set6Standard { get; set; }
        public int Set7Standard { get; set; }
        public int Set8Standard { get; set; }
        public int Set9Standard { get; set; }
        public int Set10Standard { get; set; }
        public int Set11Standard { get; set; }
        public int Set12Standard { get; set; }
        public int Set13Standard { get; set; }

        public int ScrewSpeedStandard { get; set; }
        public int ScrewCurrentStandard { get; set; }
        public int FeederSpeedStandard { get; set; }

        public Guid? EmployeeId { get; set; } // uuid, không FK cứng (sheet ghi "uuid" thôi)
    }
}
