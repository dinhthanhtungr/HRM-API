using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.SupplyRequestSchema;

namespace HRM.Domain.Entities.OrderSchema
{
    public class PurchaseOrderLink
    {
        public Guid PurchaseOrderLinkId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public Guid? MerchandiseOrderId { get; set; }
        public Guid? SupplyRequestId { get; set; }
        public bool IsActive { get; set; } = true;

        public PurchaseOrder PurchaseOrder { get; set; } = default!;
        public MerchandiseOrder? MerchandiseOrder { get; set; } = default!;
        public SupplyRequest? SupplyRequest { get; set; } = default!;   

    }
}
