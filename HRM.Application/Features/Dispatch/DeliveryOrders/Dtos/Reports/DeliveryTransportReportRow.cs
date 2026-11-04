using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports
{
    public class DeliveryTransportReportRow
    {
        public Guid DeliveryOrderId { get; set; }
        public string? DeliveryExternalId { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public string DelivererName { get; set; } = string.Empty;

        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? Receiver { get; set; }
        public string? Phone { get; set; }

        public string? ProductDisplay { get; set; }
        public string? LotNoDisplay { get; set; }
        public string? PoNoDisplay { get; set; }

        public decimal TotalQuantity { get; set; }
        public int TotalBags { get; set; }

        public decimal DeliveryPrice { get; set; }

        public string? Note { get; set; }
    }
}
