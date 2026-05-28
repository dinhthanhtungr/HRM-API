using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos
{
    public sealed class DeliveryOrderLineRequest
    {
        public Guid MerchandiseOrderDetailId { get; set; }
        public decimal Quantity { get; set; }   
        public int NumOfBags { get; set; }
        public string? LotNoList { get; set; }  
    }
}
