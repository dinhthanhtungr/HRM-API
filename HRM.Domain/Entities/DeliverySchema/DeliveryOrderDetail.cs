using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryOrderDetail
    {
        public Guid Id { get; set; }
        public Guid DeliveryOrderId { get; set; }
        public Guid? MerchandiseOrderDetailId { get; set; }
        public Guid? ProductId { get; set; }

        public string? ProductExternalIdSnapShot { get; set; }
        public string? ProductNameSnapShot { get; set; }

        public string? LotNoList { get; set; } = null;
        public string? PONo { get; set; } = null;

        public decimal Quantity { get; set; }
        public int NumOfBags { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsAttach { get; set; } = false;

        // Navigations TÙY CHỌN tương ứng
        public DeliveryOrder DeliveryOrder { get; set; } = default!;
        public MerchandiseOrderDetail? MerchandiseOrderDetail { get; set; }
        public Product? Product { get; set; }
    }
}
    