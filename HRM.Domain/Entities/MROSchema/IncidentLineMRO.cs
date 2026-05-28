using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class IncidentLineMRO
    {
        public long IncidentLineId { get; set; }        // PK (bigint)

        // Liên kết bắt buộc về incident_hdr
        public long IncidentId { get; set; }               // FK required
        public string IncidentCode { get; set; } = default!;   // lưu mã ngoài của incident

        public string? Action { get; set; }
        public string? Summary { get; set; }

        // Phiếu xuất kho liên quan (có/không đều được)
        public long? StockOutId { get; set; }                  // FK optional
        // Tham chiếu mềm tới stock_out_hdr.stock_out_code
        public string? WoRef { get; set; }

        // Người thực hiện (optional)
        public Guid? PerformedBy { get; set; }
        public DateTime? PerformedAt { get; set; }

        public string? RootCause { get; set; }
        public string? CorrectiveAction { get; set; }
        public string? PreventiveAction { get; set; }

        public int? DowntimeMinutes { get; set; }
        public decimal? CostEstimate { get; set; }

        // Navigations optional (đổi tên entity cho phù hợp dự án)
        public Employee? PerformedByEmployee { get; set; }
        public StockOutHeaderMRO? StockOut { get; set; }
        public IncidentHeaderMRO Incident { get; set; } = default!;
    }
}
