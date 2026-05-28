using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HistoryRecordSchema
{
    public class PlanGetbackHistory
    {
        public long Id { get; set; }                   // bigint identity
        public string? ExternalId { get; set; }         // citext (VA)
        public string? ColorCode { get; set; }          // citext (mã SP)
        public string? StatusBefore { get; set; }       // citext (trạng thái trước khi lấy)
        public string? Reason { get; set; }            // text (lý do lấy lệnh)
        public Guid PerformedBy { get; set; }          // uuid
        public DateTime CreatedAt { get; set; }        // timestamp
    }
}
