using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.ForscadaSchema
{
    public class ProductionStatus
    {
        public string MachineId { get; set; } = default!;
        public string? Color { get; set; }
        public string? ProductionCode { get; set; }
        public string? Note1 { get; set; }
        public string? Note2 { get; set; }
        public string? Note3 { get; set; }
        public string? Note4 { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? RequestDate { get; set; }
    }
}
