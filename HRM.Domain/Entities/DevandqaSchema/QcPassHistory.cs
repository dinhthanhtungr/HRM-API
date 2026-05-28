using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.DevandqaSchema
{
    public class QcPassHistory
    {
        public long Id { get; set; }
        public DateTime QcDate { get; set; }
        public string? MachineId { get; set; }
        public string? BatchNo { get; set; }
        public int QcRound { get; set; }
        public string? Note { get; set; }
        public Guid EmployeeId { get; set; }
        public string? StatusQc { get; set; }
    }

}
