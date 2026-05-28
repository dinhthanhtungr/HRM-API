using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Notifications
{
    public enum TopicNotifications
    {
        // ==================== Products ==================== 
        ProductSampleCreated,
        ProductSampleUpdated,
        ProductSampleDeleted,
        SampleRequestUpdated,

        // ==================== Merchandise Orders ====================
        MerchandiseOrderCreated,
        MerchandiseOrderUpdated,
        MerchandiseOrderDeleted,

        // ==================== Merchandise Orders ====================
        ManufacturingOrderCreated,
        ManufacturingOrderUpdated,
        ManufacturingOrderDeleted,

        // ==================== Price Over Sell ====================
        PriceOverSellCreated,
        WarehouseStockLost,

        // ==================== MfgProduction Orders ====================
        MfgProductionOrderChangeExpectiveDate,
        MfgProductionOrderUpdated,
        MfgProductionOrderDeleted
    }
}
