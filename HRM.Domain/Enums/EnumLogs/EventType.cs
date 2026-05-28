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
    }
}
