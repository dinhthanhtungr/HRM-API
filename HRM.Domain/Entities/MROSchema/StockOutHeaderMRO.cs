using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class StockOutHeaderMRO
    {
        public long StockOutId { get; set; }                 // bigint identity (PK)
        public string StockOutCode { get; set; } = string.Empty; // text (unique per factory)
        public string Status { get; set; } = "Draft";      // text: Draft/Posted/Cancelled
        public string? Reason { get; set; }                 // text
        public string? Note { get; set; }                 // text

        public Guid FactoryId { get; set; }                 // uuid (FK -> Companies/Factories tùy bạn)
        public string? FactoryCode { get; set; }                 // text (mã external của factory, không FK)

        public string? SourceType { get; set; }                 // text (polymorphic)
        public long? SourceId { get; set; }                 // bigint (id nguồn, không FK cứng)
        public string? SourceCode { get; set; }                 // text (mã nguồn)

        public DateTime CreatedAt { get; set; }                 // timestamptz (UTC)
        public Guid CreatedBy { get; set; }                 // uuid (FK -> hr.Employees)

        public DateTime? PostedAt { get; set; }                 // timestamptz (UTC)
        public Guid? PostedBy { get; set; }                 // uuid (FK -> hr.Employees)

        // (Tùy: thêm navigation nếu có)
        public virtual Company? Factory { get; set; }
        public virtual Employee? CreatedByNav { get; set; }
        public virtual Employee? PostedByNav { get; set; }
        public virtual IncidentHeaderMRO? IncidentHeaderMRO { get; set; }
    }
}
