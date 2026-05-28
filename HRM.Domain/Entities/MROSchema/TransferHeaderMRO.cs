using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class TransferHeaderMRO
    {
        public long TransferHeaderId { get; set; }         // bigint PK
        public string TransferHeaderExternalId { get; set; } = "";  // text
        public string? Note { get; set; }         // text
        public DateTime CreatedAt { get; set; }         // datetime (UTC)
        public Guid CreatedBy { get; set; }         // uuid (FK -> Employees)
        public bool Posted { get; set; }         // boolean
        public DateTime? PostedAt { get; set; }         // datetime
        public Guid? PostedBy { get; set; }         // uuid (FK -> Employees)

        public virtual Employee? Creator { get; set; }
        public virtual Employee? Postor { get; set; }
        public virtual ICollection<TransferDetailMRO> Details { get; set; } = new List<TransferDetailMRO>();
    }
}
