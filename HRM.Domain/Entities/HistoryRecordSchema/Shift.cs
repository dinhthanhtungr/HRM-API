using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class Shift
    {
        public short ShiftId { get; set; }     // smallint PK
        public string? ShiftName { get; set; }     // text
        public string? Note { get; set; }     // text
    }
}
