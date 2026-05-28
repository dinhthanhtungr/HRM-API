using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class MfgOrderPO
    {
        public Guid MerchandiseOrderDetailId { get; set; }
        public Guid MfgProductionOrderId { get; set; }
        public bool IsActive { get; set; }

        public MerchandiseOrderDetail Detail { get; set; } = default!;
        public MfgProductionOrder ProductionOrder { get; set; } = default!;
    }
}
