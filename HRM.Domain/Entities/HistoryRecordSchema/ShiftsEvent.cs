using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class ShiftsEvent
    {
        public short EventId { get; set; }   // smallint PK
        public string? EventIdName { get; set; }   // text
        public string? Note { get; set; }   // text
    }
}
