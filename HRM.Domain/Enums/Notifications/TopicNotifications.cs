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
        ProductSampleCreated = 0,
        ProductSampleUpdated = 1,
        ProductSampleDeleted = 2,
        SampleRequestUpdated = 3,

        // ==================== Merchandise Orders ====================
        MerchandiseOrderCreated = 4,
        MerchandiseOrderUpdated = 5,
        MerchandiseOrderDeleted = 6,

        // ==================== Merchandise Orders ====================
        ManufacturingOrderCreated = 7,
        ManufacturingOrderUpdated = 8,
        ManufacturingOrderDeleted = 9,

        // ==================== Price Over Sell ====================
        PriceOverSellCreated = 10,
        WarehouseStockLost = 11,

        // ==================== Customer CRM ====================
        CustomerLeadAssigned = 12,
        CustomerFollowUpTaskAssigneeAdded = 13,
        CustomerFollowUpTaskDue = 14,

        // ==================== Work Management ====================
        WorkTaskAssigneeAdded = 15,
        WorkTaskDue = 16,
        WorkPlanAssigneeAdded = 17,

        // ==================== MfgProduction Orders ====================
        MfgProductionOrderChangeExpectiveDate = 18,
        MfgProductionOrderUpdated = 19,
        MfgProductionOrderDeleted = 20,
        ManufacturingFormulaAdjustmentCreated = 21,

        // Append-only: topic duoc luu dang enum so, khong chen vao giua de tranh lam lech du lieu cu.
        SampleRequestMessageCreated = 22,
        SampleRequestPriceQuoteRequested = 23,
        SampleRequestChangeRequested = 24,
        SampleRequestUpdateRequested = 25,
        InternalMailMessageCreated = 26
    }
}
