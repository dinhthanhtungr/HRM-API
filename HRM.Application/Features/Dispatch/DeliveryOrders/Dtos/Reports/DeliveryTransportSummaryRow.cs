using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports
{
    public class DeliveryTransportSummaryRow
    {
        public int Stt { get; set; }
        public string DelivererName { get; set; } = string.Empty;

        public int TotalTrips { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }

        public decimal AdvanceAmount { get; set; } = 0; // hiện chưa có nguồn dữ liệu
        public decimal RemainingAmount => TotalAmount - AdvanceAmount;

        public string? Note { get; set; }
    }
}
