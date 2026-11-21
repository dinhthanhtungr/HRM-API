namespace HRM.Application.Commons.Authorization;

/// <summary>Danh mục capability thực sự được backend hỗ trợ, dùng cho trình cấu hình role.</summary>
public static class ApplicationPermissionCatalog
{
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        ApplicationPermissions.CRM.ManageCustomerSampleSharing,
        ApplicationPermissions.CRM.AdminCustomerSampleSharing,
        ApplicationPermissions.PLM.ViewSampleRequestTracking,
        ApplicationPermissions.PLM.ViewSaleOrderTracking,
        ApplicationPermissions.PLM.ViewProductionTracking,
        ApplicationPermissions.PLM.ViewSampleProductionTracking,
        ApplicationPermissions.Warehouse.ViewStockTracking,
        ApplicationPermissions.Dispatch.ViewDeliveryTracking,
        ApplicationPermissions.Reports.ViewDeliveryOtd,
        ApplicationPermissions.Reports.ViewDeliveryFinish,
        ApplicationPermissions.CRM.ViewCustomerTracking,
        ApplicationPermissions.Quotations.ViewTracking,
        ApplicationPermissions.PLM.EditRequestedDeliverySchedule,
        ApplicationPermissions.PLM.EditPromisedDeliverySchedule,
        ApplicationPermissions.Warehouse.ViewVoucherSuppliers,
        ApplicationPermissions.PLM.ViewMaterialProcessing,
        ApplicationPermissions.PLM.CreateMaterialProcessing,
        ApplicationPermissions.PLM.ManageMaterialProcessing,
        ApplicationPermissions.PLM.ManageSampleWorkSchedule,
        ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs,
        ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs,
        ApplicationPermissions.PLM.ManageLegacySampleWork,
        ApplicationPermissions.PLM.ViewAllLabTeamWork,
        ApplicationPermissions.PLM.CorrectSampleTrialFeedback,
        ApplicationPermissions.PLM.ManageSampleLabWorkflow,
        ApplicationPermissions.PLM.ManageSampleSaleWorkflow,
        ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow,
        ApplicationPermissions.PLM.DeactivateSampleRequests,
        ApplicationPermissions.PLM.ConfirmCustomerPrice,
        ApplicationPermissions.PLM.ReceiveLabStandard,
        ApplicationPermissions.Warehouse.ViewMixingStock,
        ApplicationPermissions.Reports.ViewSalesProfit,
        ApplicationPermissions.Reports.ViewPlpu,
        ApplicationPermissions.DevAndQA.ViewIqc,
        ApplicationPermissions.DevAndQA.ManageIqc,
        ApplicationPermissions.DevAndQA.ViewCoa,
        ApplicationPermissions.DevAndQA.ManageCoa,
        ApplicationPermissions.InternalMail.DeleteMessage,
        ApplicationPermissions.InternalMail.ObserveCompanyConversations,
        ApplicationPermissions.InternalMail.SendCompanyMessages,
        ApplicationPermissions.InternalMail.ManageDefaultMembershipPolicies,
        // GROUP-STORAGE-TRANSITION: synchronization-api — temporary catalog entry for the maintenance endpoint.
        ApplicationPermissions.InternalMail.SynchronizeGroupStorage,
        ApplicationPermissions.Equipment.View,
        ApplicationPermissions.Equipment.Create,
        ApplicationPermissions.Equipment.Update,
        ApplicationPermissions.Equipment.Delete,
        ApplicationPermissions.Pricing.ViewWorkbench,
        ApplicationPermissions.Pricing.ViewApprovedSellingPrice,
        ApplicationPermissions.Pricing.ViewSystemCalculatedPrice,
        ApplicationPermissions.Pricing.ViewMaterialCost,
        ApplicationPermissions.Pricing.ViewManufacturingCost,
        ApplicationPermissions.Pricing.ViewMargin,
        ApplicationPermissions.Pricing.ViewHistory,
        ApplicationPermissions.Pricing.Manage,
        ApplicationPermissions.Pricing.Approve,
        ApplicationPermissions.PLM.CreateProductionOrders,
        ApplicationPermissions.PLM.ExtractMaterialDocuments,
        ApplicationPermissions.PLM.ViewProductionOrders,
        ApplicationPermissions.PLM.CheckProductionOrders,
        ApplicationPermissions.PLM.ManageProductionOrders,
        ApplicationPermissions.PLM.EditProductionOrderRequirement,
        ApplicationPermissions.PLM.ViewFormulaPrices,
        ApplicationPermissions.PLM.ViewFormulaMaterials,
        ApplicationPermissions.PLM.ViewManufacturingDefectiveLots,
        ApplicationPermissions.PLM.ViewProductTechnicalInfo,
        ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails,
        ApplicationPermissions.Dispatch.ViewDeliveryCost,
        ApplicationPermissions.Purchasing.ManagePurchaseOrders,
        ApplicationPermissions.Purchasing.ManageCatalog
    });

    public static IReadOnlyList<string> ResolveRole(
        string roleName, bool usesDatabasePermissions, IEnumerable<string> claims)
        => usesDatabasePermissions
            ? claims.Where(code => Codes.Contains(code, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : Codes.Where(code => ApplicationPermissionRoleSets.GetRoles(code)
                .Contains(roleName, StringComparer.OrdinalIgnoreCase)).ToArray();
}
