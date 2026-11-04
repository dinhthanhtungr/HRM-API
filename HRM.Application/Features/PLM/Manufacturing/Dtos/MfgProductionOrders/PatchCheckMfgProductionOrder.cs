using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders
{
    public class PatchCheckMfgProductionOrder
    {
        public Guid mfgProductionOrderId { get; set; }
        public bool? IsPrintedStock { get; set; } = false; // Đã lưu trữ công thức
    }
}
