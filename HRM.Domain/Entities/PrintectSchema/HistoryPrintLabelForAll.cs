using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.PrintectSchema
{
    public class HistoryPrintLabelForAll
    {
        public long Id { get; set; }                     // historyprintlabelforall_id
        public string? ExternalId { get; set; }           // liên kết mềm, không FK
        public int NumberOfCopies { get; set; }          // số bản in
        public DateTime CreatedAt { get; set; } = DateTime.Now;         // thời điểm in thực tế
        public short? ShiftId { get; set; }              // ca in
        public int? LogNumber { get; set; }            // số lô in cho mã
        public DateTime? ProductionDate { get; set; }    // ngày SX (nếu in lại)
    }
}
