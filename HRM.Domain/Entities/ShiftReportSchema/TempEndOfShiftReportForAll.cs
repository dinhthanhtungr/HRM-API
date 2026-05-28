using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.ShiftReportSchema
{
    public class TempEndOfShiftReportForAll
    {
        public long TempShiftReportForAllId { get; set; } // PK (bigint)
        public DateTime CreatedAt { get; set; } = DateTime.Now;           // datetime
        public short ShiftId { get; set; }                // smallint
        public Guid MachineId { get; set; }               // uuid
        public Guid Operator { get; set; }                // uuid
        public string? ProductCode { get; set; }          // text/citext
        public string? ExternalId { get; set; }           // text/citext
        public string? Note { get; set; }                 // text
        public string? ProductStatus { get; set; }        // text
    }
}
