namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Tên capability ổn định dùng trong Application. Role chỉ được ánh xạ vào các capability này
/// tại source of truth của authorization, không được dùng thay cho permission nghiệp vụ.
/// </summary>
public static class ApplicationPermissions
{
    public static class Warehouse
    {
        public const string ViewMixingStock = "warehouse.mixing-stock.view";
    }

    public static class Quotations
    {
        public const string ConfirmSent = "crm.quotation.send.confirm";
    }

    public static class Reports
    {
        public const string ViewPlpu = "reports.plpu.view";
        public const string ViewSalesProfit = "reports.sales-profit.view";
    }

    public static class DevAndQA
    {
        public const string ViewWorkflowNotificationJobs = "dev.workflow-notification-jobs.view";
        public const string RetryWorkflowNotificationJobs = "dev.workflow-notification-jobs.retry";
        public const string ViewIqc = "dev-and-qa.iqc.view";
        public const string ManageIqc = "dev-and-qa.iqc.manage";
        public const string ViewCoa = "dev-and-qa.coa.view";
        public const string ManageCoa = "dev-and-qa.coa.manage";
    }

    public static class InternalMail
    {
        public const string ManageDefaultMembershipPolicies = "internal-mail.default-membership.manage";
        // GROUP-STORAGE-TRANSITION: synchronization-api — temporary technical administration capability.
        public const string SynchronizeGroupStorage = "internal-mail.group-storage.synchronize";
        public const string ObserveCompanyConversations = "internal-mail.company-conversations.observe";
        public const string SendCompanyMessages = "internal-mail.company-messages.send";
        public const string DeleteMessage = "internal-mail.message.delete";
        public const string ObservePricingGroup = "internal-mail.pricing-group.observe";
        public const string ObserveSampleRequestGroups = "internal-mail.sample-request-groups.observe";
    }

    public static class Equipment
    {
        public const string View = "mro.equipment.view";
        public const string Create = "mro.equipment.create";
        public const string Update = "mro.equipment.update";
        public const string Delete = "mro.equipment.delete";
    }

    public static class Pricing
    {
        public const string ViewWorkbench = "pricing.workbench.view";
        public const string ViewApprovedSellingPrice = "pricing.approved-selling-price.view";
        public const string ViewSystemCalculatedPrice = "pricing.system-calculated-price.view";
        public const string ViewMaterialCost = "pricing.material-cost.view";
        public const string ViewManufacturingCost = "pricing.manufacturing-cost.view";
        public const string ViewMargin = "pricing.margin.view";
        public const string ViewHistory = "pricing.history.view";
        public const string Manage = "pricing.manage";
        public const string Approve = "pricing.approve";
    }

    public static class PLM
    {
        public const string ManageSampleWorkSchedule = "plm.sample-request.work-schedule.manage";
        public const string ManageSampleLabWorkflow = "plm.sample-request.workflow.lab";
        public const string ManageSampleSaleWorkflow = "plm.sample-request.workflow.sale";
        public const string ManageManagedSampleSaleWorkflow = "plm.sample-request.workflow.sale.managed";
        public const string CorrectSampleTrialFeedback = "plm.sample-trial.feedback.correct";
        public const string ManageLegacySampleWork = "plm.sample-request.legacy-work.manage";
        public const string ViewAllLabTeamWork = "plm.sample-request.lab-teams.view-all";
        public const string DeactivateSampleRequests = "plm.sample-request.deactivate";
        public const string ConfirmCustomerPrice = "plm.sample-request.customer-price.confirm";
        public const string ReceiveLabStandard = "plm.sample-request.lab-standard.receive";
        public const string ExtractMaterialDocuments = "plm.material-document.extract";
        public const string ViewProductionOrders = "plm.production-order.view";
        public const string CheckProductionOrders = "plm.production-order.check";
        public const string ManageProductionOrders = "plm.production-order.manage";
        public const string EditProductionOrderRequirement = "plm.production-order.requirement.edit";
        public const string ManageManufacturingFormulas = "plm.manufacturing-formula.manage";
        public const string ManageManufacturingAdjustments = "plm.manufacturing-adjustment.manage";
        public const string ManageManufacturingColorChips = "plm.manufacturing-color-chip.manage";
        public const string CreateProductionOrders = "plm.production-order.create";
        public const string ViewFormulaPrices = "plm.formula-price.view";
        public const string ViewFormulaMaterials = "plm.formula-material.view";
        public const string ViewManufacturingDefectiveLots = "plm.manufacturing-defective-lot.view";
        public const string ViewProductTechnicalInfo = "plm.product-technical-info.view";
        public const string ViewMaterialPriceReviewDetails = "plm.material-price-review.detail.view";
    }

    public static class Dispatch
    {
        public const string ViewDeliveryCost = "dispatch.delivery-cost.view";
    }

    public static class Purchasing
    {
        public const string ManagePurchaseOrders = "purchasing.purchase-order.manage";
        public const string ManageCatalog = "purchasing.catalog.manage";
    }

}
