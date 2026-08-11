using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Logs
{
    public enum EventType
    {
        ManufacturingProductOrder = 1,
        DeliveryOrderStatus = 2,
        ManufacturingProductOrderFormula = 3,
        MerchadiseStatus = 4,
        SamplerStatus = 5,
        SupplyRequestStatus = 6,
        PLMChangeOrder = 7,
        PLMApproval = 8,
        QuotationStatus = 9,
        PurchaseOrderStatus = 10,
        CustomerCrm = 11,
        ComplaintReport = 12,
    }
}
