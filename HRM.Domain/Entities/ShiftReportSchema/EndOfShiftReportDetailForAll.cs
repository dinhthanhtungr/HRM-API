using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.ShiftReportSchema
{
    public class EndOfShiftReportDetailForAll
    {
        public long ShiftReportDetailForAllId { get; set; } // PK
        public string? ExternalId { get; set; }
        public string? ProductType { get; set; }
        public decimal? NetWeight { get; set; }
        public decimal? WeightStockedKg { get; set; }

        public long? ShiftReportForAllId { get; set; } // FK
        public EndOfShiftReportForAll? ShiftReportForAll { get; set; } // Navigation property
    }
}
