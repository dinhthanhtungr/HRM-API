using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryOrderPO
    {
        public Guid DeliveryOrderId { get; set; }
        public Guid MerchandiseOrderId { get; set; }
        public bool IsActive { get; set; } = true;
        public DeliveryOrder DeliveryOrder { get; set; } = default!;
        public MerchandiseOrder MerchandiseOrder { get; set; } = default!;
    }
}
