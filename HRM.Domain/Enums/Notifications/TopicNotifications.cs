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
        InternalMailMessageCreated = 26,
        SampleRequestUpdateApproved = 27,
        SampleRequestUpdateRejected = 28,

        // Topic từ đây tuân theo cấu trúc nghiệp vụ cụ thể; các giá trị 0-28 được giữ nguyên tuyệt đối.
        SampleRequestDataChangeRequested = 29,
        SampleRequestDataChangeApproved = 30,
        SampleRequestDataChangeRejected = 31,
        MerchandiseOrderDeliveryPaused = 32,
        MerchandiseOrderDeliveryResumed = 33,
        QuotationSent = 34,
        QuotationMessageCreated = 35,
        QuotationRequested = 36,
        SampleRequestSampleSent = 37,
        SampleRequestFormulaCompleted = 38,
        SampleRequestFormulaUpdateRequested = 39,
        SampleRequestFormulaUpdateApproved = 40,
        SampleRequestFormulaUpdateRejected = 41,
        SampleRequestFormulaUpdateCancelled = 42,
        SampleRequestDirectPatchNotified = 43,

        // ==================== Developer Operations ====================
        CustomerAiSummaryAutomationStatus = 44,
        ComplaintInitialDecision = 45,
        ComplaintFinalDecision = 46,
        SampleRequestCustomerFeedbackRecorded = 47
    }
}
