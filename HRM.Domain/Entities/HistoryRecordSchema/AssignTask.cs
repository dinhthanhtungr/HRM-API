using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class AssignTask
    {
        public long AssignId { get; set; }   // bigint PK
        public DateTime CreatedAt { get; set; } = DateTime.Now;  // timestamp
        public short ShiftId { get; set; }   // smallint
        public string MachineId { get; set; } = string.Empty; // text (theo spec)
        public Guid? OperatorId { get; set; }  // uuid
        public string? Note { get; set; }   // text
    }
}
