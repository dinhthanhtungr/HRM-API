using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.SupplyRequestSchema
{
    public partial class SupplyRequestDetail
    {
        public int DetailId { get; set; }              // PK (int identity)
        public Guid SupplyRequestId { get; set; }      // FK -> SupplyRequest
        public Guid MaterialId { get; set; }           // FK -> Material
        public int RequestedQuantity { get; set; }     // > 0
        public int? RealQuantity { get; set; }     // > 0
        public string? Note { get; set; }              // NVARCHAR(MAX)

        public DateTime? ReceptDate { get; set; }        // NULL

        public virtual SupplyRequest SupplyRequest { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
