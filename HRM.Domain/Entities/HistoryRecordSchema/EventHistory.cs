using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class EventHistory
    {
        public long Id { get; set; }                 // EventHistory_Id
        public string? MachineId { get; set; }        // text (nếu cần CI: citext ở Fluent)
        public short EventId { get; set; }           // smallint
        public DateTime CreatedAt { get; set; }  = DateTime.Now;    // timestamp
    }
}
