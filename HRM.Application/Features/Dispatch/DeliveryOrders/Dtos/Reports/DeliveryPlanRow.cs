using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports
{
    public sealed class DeliveryPlanRow
    {
        public int Stt { get; set; }
        public string CustomerName { get; set; } = "";
        public string Code { get; set; } = "";
        public string LotNo { get; set; } = "";
        public string QuantityText { get; set; } = "";  // "500*" hoặc "250"
        public bool QuantityIsPending { get; set; }     // để tô đỏ
        public string Factory { get; set; } = "";
        public string PickupTimeText { get; set; } = ""; // "BUỔI SÁNG"
        public string Driver { get; set; } = "";
        public string Note { get; set; } = "";
        public string Address { get; set; } = "";
        public string ManufacturingFormulaExternalIds { get; set; } = ""; // ví dụ: "VA001, VA002"

        public DateTime? ExpectedDeliveryDate { get; set; }
    }
}
