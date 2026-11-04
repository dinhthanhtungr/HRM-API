namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Ánh xạ tập trung từ capability sang role hiện hành. Đây là điểm thay thế khi chuyển
/// sang permission lưu trong database mà không làm thay đổi các feature đang sử dụng capability.
/// </summary>
public static class ApplicationPermissionRoleSets
{
    public static IReadOnlyCollection<string> GetRoles(string permission)
        => permission switch
        {
            ApplicationPermissions.PLM.ManageSampleWorkSchedule => ApplicationRoleSets.PLM.SampleWorkScheduleManagers,
            ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs => ApplicationRoleSets.DevAndQA.WorkflowNotificationJobAdministrators,
            ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs => ApplicationRoleSets.DevAndQA.WorkflowNotificationJobAdministrators,
            ApplicationPermissions.PLM.CorrectSampleTrialFeedback => ApplicationRoleSets.PLM.SampleTrialFeedbackCorrectors,
            ApplicationPermissions.PLM.ManageSampleLabWorkflow => ApplicationRoleSets.PLM.SampleRequestLabWorkflowEditors,
            ApplicationPermissions.PLM.ManageSampleSaleWorkflow => ApplicationRoleSets.PLM.SampleSaleWorkflowEditors,
            ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow => ApplicationRoleSets.PLM.ManagedSampleSaleWorkflowEditors,
            ApplicationPermissions.PLM.ManageLegacySampleWork => ApplicationRoleSets.Reports.SampleWorkflowAdministrators,
            ApplicationPermissions.PLM.ViewAllLabTeamWork => ApplicationRoleSets.Reports.LabTeamWorkObservers,
            ApplicationPermissions.InternalMail.ManageDefaultMembershipPolicies => ApplicationRoleSets.InternalMail.DefaultMembershipPolicyManagers,
            // GROUP-STORAGE-TRANSITION: synchronization-api — remove together with its technical capability.
            ApplicationPermissions.InternalMail.SynchronizeGroupStorage => ApplicationRoleSets.InternalMail.GroupStorageAdministrators,
            ApplicationPermissions.PLM.ConfirmCustomerPrice => ApplicationRoleSets.PLM.SampleSaleWorkflowEditors,
            ApplicationPermissions.PLM.ReceiveLabStandard => ApplicationRoleSets.PLM.LabStandardReceivers,
            ApplicationPermissions.PLM.DeactivateSampleRequests => ApplicationRoleSets.PLM.SampleRequestDeactivators,
            ApplicationPermissions.Warehouse.ViewMixingStock => ApplicationRoleSets.Warehouse.MixingStockViewers,
            ApplicationPermissions.Reports.ViewPlpu => ApplicationRoleSets.Reports.PlpuReaders,
            ApplicationPermissions.Reports.ViewSalesProfit => ApplicationRoleSets.Reports.SalesProfitViewers,
            ApplicationPermissions.DevAndQA.ViewIqc => ApplicationRoleSets.DevAndQA.IqcViewers,
            ApplicationPermissions.DevAndQA.ManageIqc => ApplicationRoleSets.DevAndQA.IqcManagers,
            ApplicationPermissions.DevAndQA.ViewCoa => ApplicationRoleSets.DevAndQA.CoaViewers,
            ApplicationPermissions.DevAndQA.ManageCoa => ApplicationRoleSets.DevAndQA.CoaManagers,
            ApplicationPermissions.InternalMail.DeleteMessage => ApplicationRoleSets.InternalMail.MessageDeleters,
            ApplicationPermissions.InternalMail.ObserveCompanyConversations => ApplicationRoleSets.InternalMail.CompanyConversationObservers,
            ApplicationPermissions.InternalMail.SendCompanyMessages => ApplicationRoleSets.InternalMail.CompanyMessageSenders,
            ApplicationPermissions.InternalMail.ObservePricingGroup => ApplicationRoleSets.InternalMail.PricingObservers,
            ApplicationPermissions.InternalMail.ObserveSampleRequestGroups => ApplicationRoleSets.InternalMail.SampleRequestGroupObservers,
            ApplicationPermissions.Equipment.View => ApplicationRoleSets.Equipment.Viewers,
            ApplicationPermissions.Equipment.Create => ApplicationRoleSets.Equipment.Editors,
            ApplicationPermissions.Equipment.Update => ApplicationRoleSets.Equipment.Editors,
            ApplicationPermissions.Equipment.Delete => ApplicationRoleSets.Equipment.Deleters,
            ApplicationPermissions.Pricing.ViewWorkbench =>
                ApplicationRoleSets.Pricing.WorkbenchViewers,
            ApplicationPermissions.Pricing.ViewApprovedSellingPrice =>
                ApplicationRoleSets.Pricing.ApprovedSellingPriceViewers,
            ApplicationPermissions.Pricing.ViewSystemCalculatedPrice =>
                ApplicationRoleSets.Pricing.SystemCalculatedPriceViewers,
            ApplicationPermissions.Pricing.ViewMaterialCost =>
                ApplicationRoleSets.Pricing.MaterialCostViewers,
            ApplicationPermissions.Pricing.ViewManufacturingCost =>
                ApplicationRoleSets.Pricing.ManufacturingCostViewers,
            ApplicationPermissions.Pricing.ViewMargin =>
                ApplicationRoleSets.Pricing.MarginViewers,
            ApplicationPermissions.Pricing.ViewHistory =>
                ApplicationRoleSets.Pricing.HistoryViewers,
            ApplicationPermissions.Pricing.Manage =>
                ApplicationRoleSets.Pricing.Managers,
            ApplicationPermissions.Pricing.Approve =>
                ApplicationRoleSets.Pricing.Approvers,
            ApplicationPermissions.PLM.CreateProductionOrders => ApplicationRoleSets.PLM.ProductionOrderCreators,
            ApplicationPermissions.PLM.ExtractMaterialDocuments => ApplicationRoleSets.PLM.MaterialDocumentExtractors,
            ApplicationPermissions.PLM.ViewProductionOrders => ApplicationRoleSets.PLM.ProductionOrderViewers,
            ApplicationPermissions.PLM.CheckProductionOrders => ApplicationRoleSets.PLM.ProductionOrderCheckers,
            ApplicationPermissions.PLM.ManageProductionOrders => ApplicationRoleSets.PLM.ProductionOrderManagers,
            ApplicationPermissions.PLM.EditProductionOrderRequirement => ApplicationRoleSets.PLM.ProductionOrderRequirementEditors,
            ApplicationPermissions.PLM.ManageManufacturingFormulas => ApplicationRoleSets.PLM.ManufacturingFormulaEditors,
            ApplicationPermissions.PLM.ManageManufacturingAdjustments => ApplicationRoleSets.PLM.ManufacturingAdjustmentEditors,
            ApplicationPermissions.PLM.ManageManufacturingColorChips => ApplicationRoleSets.PLM.ManufacturingColorChipEditors,
            ApplicationPermissions.PLM.ViewFormulaPrices =>
                ApplicationRoleSets.PLM.FormulaPriceViewers,
            ApplicationPermissions.PLM.ViewFormulaMaterials =>
                ApplicationRoleSets.PLM.FormulaMaterialViewers,
            ApplicationPermissions.PLM.ViewManufacturingDefectiveLots => ApplicationRoleSets.PLM.ManufacturingDefectiveLotViewers,
            ApplicationPermissions.PLM.ViewProductTechnicalInfo =>
                ApplicationRoleSets.PLM.ProductTechnicalViewers,
            ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails =>
                ApplicationRoleSets.PLM.MaterialPriceReviewDetailViewers,
            ApplicationPermissions.Dispatch.ViewDeliveryCost =>
                ApplicationRoleSets.Dispatch.DeliveryCostViewers,
            ApplicationPermissions.Purchasing.ManagePurchaseOrders =>
                ApplicationRoleSets.Modules.Purchasing,
            ApplicationPermissions.Purchasing.ManageCatalog =>
                ApplicationRoleSets.Modules.Purchasing,
            ApplicationPermissions.Quotations.ConfirmSent => ApplicationRoleSets.Quotations.SendConfirmers,
            _ => []
        };
}
