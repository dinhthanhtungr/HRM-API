using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.ShiftReportSchema
{
    public class OperationHistoryMd
    {
        public DateTime CreatedAt { get; set; }         // created_at (UTC)
        public string? ProductCode { get; set; }         // productcode
        public string? ExternalId { get; set; }          // externalId

        public int? Set1 { get; set; }
        public int? Act1 { get; set; }
        public int? Set2 { get; set; }
        public int? Act2 { get; set; }
        public int? Set3 { get; set; }
        public int? Act3 { get; set; }
        public int? Set4 { get; set; }
        public int? Act4 { get; set; }
        public int? Set5 { get; set; }
        public int? Act5 { get; set; }
        public int? Set6 { get; set; }
        public int? Act6 { get; set; }
        public int? Set7 { get; set; }
        public int? Act7 { get; set; }
        public int? Set8 { get; set; }
        public int? Act8 { get; set; }
        public int? Set9 { get; set; }
        public int? Act9 { get; set; }
        public int? Set10 { get; set; }
        public int? Act10 { get; set; }
        public int? Set11 { get; set; }
        public int? Act11 { get; set; }
        public int? Set12 { get; set; }
        public int? Act12 { get; set; }
        public int? Set13 { get; set; }
        public int? Act13 { get; set; }

        public int? ScrewSpeed { get; set; }
        public int? ScrewCurrent { get; set; }
        public int? FeederSpeed { get; set; }

        public string? MachineId { get; set; }
    }
}
