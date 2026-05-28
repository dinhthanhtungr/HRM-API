using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.Notifications
{
    public class OutboxMessage
    {
        public long Id { get; set; }                 // Identity
        public string Type { get; set; } = default!; // ví dụ: "PriceExceededEvent"
        public string PayloadJson { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ProcessedAt { get; set; }
        public int Attempts { get; set; } = 0;
        public string? Error { get; set; }
    }
}
