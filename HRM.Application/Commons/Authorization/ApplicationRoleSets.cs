namespace HRM.Application.Commons.Authorization;

public static class ApplicationRoleSets
{
    public static class SaleOrderDelivery
    {
        public static readonly string[] SalesPausers = [ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Sales.SaleAdmin, ApplicationRoles.Sales.ACUser];
        public static readonly string[] Pausers = [ApplicationRoles.Admin, ApplicationRoles.President,
            ApplicationRoles.Developer, ApplicationRoles.Leader, .. SalesPausers, ApplicationRoles.Accounting.ACCUser];
        public static readonly string[] CompanyPausers = [ApplicationRoles.Admin, ApplicationRoles.President,
            ApplicationRoles.Developer, ApplicationRoles.Accounting.ACCUser];
    }
    public static class Warehouse
    {
        public static readonly string[] MixingStockViewers =
            [ApplicationRoles.Production.PLPUUser, ApplicationRoles.Developer, ApplicationRoles.President];
    }

    public static class Quotations
    {
        public static readonly string[] SendConfirmers = [ApplicationRoles.President, ApplicationRoles.Developer];
    }

    public static class Reports
    {
        public static readonly string[] LabTeamWorkObservers =
            [ApplicationRoles.Lab.LabAdmin, ApplicationRoles.Lab.LabUser];
        public const string SampleWorkflowAdministratorRolesCsv = ApplicationRoles.Admin;
        public static readonly string[] SampleWorkflowAdministrators = [ApplicationRoles.Admin];
        public static readonly string[] PlpuReaders =
            [ApplicationRoles.Developer, ApplicationRoles.President, ApplicationRoles.Production.PLPUUser];

        public const string SalesScopedReaderRolesCsv =
            $"{ApplicationRoles.Developer},{ApplicationRoles.President},{ApplicationRoles.Accounting.ACCUser},{ApplicationRoles.Sales.SaleUser},{ApplicationRoles.Sales.SaleAdmin},{ApplicationRoles.Sales.ACUser}";

        public static readonly string[] SalesScopedReaders =
            [ApplicationRoles.Developer, ApplicationRoles.President, ApplicationRoles.Accounting.ACCUser,
                ApplicationRoles.Sales.SaleUser, ApplicationRoles.Sales.SaleAdmin, ApplicationRoles.Sales.ACUser];

        public const string SalesDashboardReaderRolesCsv =
            $"{ApplicationRoles.Developer},{ApplicationRoles.President}";

        public static readonly string[] SalesDashboardReaders =
            [ApplicationRoles.Developer, ApplicationRoles.President];

        public static readonly string[] SalesCompanyReaders =
            [ApplicationRoles.President, ApplicationRoles.Accounting.ACCUser, ApplicationRoles.Developer];

        public static readonly string[] SalesProfitViewers =
            [ApplicationRoles.President, ApplicationRoles.Accounting.ACCUser, ApplicationRoles.Developer];
    }

    public static class DevAndQA
    {
        public static readonly string[] WorkflowNotificationJobAdministrators = [ApplicationRoles.Developer];
        public static readonly string[] CatalogSyncManagers = [ApplicationRoles.Developer, ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
        public static readonly string[] NonDraftTemplateEditors = [ApplicationRoles.Developer, ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
        public static readonly string[] TemplateDefinitionManagers = [ApplicationRoles.Developer];

        public static readonly string[] CoaViewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser, ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];

        public static readonly string[] CoaManagers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President, ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];

        public static readonly string[] IqcViewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.Quality.QCUser, ApplicationRoles.Warehouse.KHOUser, ApplicationRoles.Lab.LabUser];

        public static readonly string[] IqcManagers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
    }

    public static class InternalMail
    {
        public static readonly string[] DefaultMembershipPolicyManagers = [ApplicationRoles.Developer];
        // GROUP-STORAGE-TRANSITION: synchronization-api — technical migration only; does not alter chat permissions.
        public static readonly string[] GroupStorageAdministrators = [ApplicationRoles.Developer];
        public static readonly string[] CompanyConversationObservers = [ApplicationRoles.Developer];
        public static readonly string[] CompanyMessageSenders = [ApplicationRoles.Developer];
        public static readonly string[] MessageDeleters = [ApplicationRoles.Developer];
        public static readonly string[] PricingMembers = [ApplicationRoles.Sales.SaleUser, ApplicationRoles.Sales.SaleAdmin];
        public static readonly string[] PricingExecutives = [ApplicationRoles.President];
        public static readonly string[] TechnicalMembers = [ApplicationRoles.Lab.LabUser, ApplicationRoles.President];
        public static readonly string[] PricingObservers = [ApplicationRoles.Developer];
        public static readonly string[] SampleRequestGroupObservers = [ApplicationRoles.Developer];
    }

    public static class Equipment
    {
        public static readonly string[] Viewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.Maintenance.MaintenanceUser,
            ApplicationRoles.Production.ManufactureUser, ApplicationRoles.Production.QLSXUser];
        public static readonly string[] Editors =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Maintenance.MaintenanceUser];
        public static readonly string[] Deleters =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President];
    }

    public static readonly string[] SuperUsers =
    [
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    public static readonly string[] Managers =
    [
        ApplicationRoles.Leader,
        ApplicationRoles.President
    ];

    public static readonly string[] PriceReaders =
    [
        ApplicationRoles.Sales.PriceView,
        ApplicationRoles.Purchasing.Purchaser,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President,
        ApplicationRoles.Accounting.ACCUser,
        ApplicationRoles.SeePrice.SeePriceUser
    ];

    public static readonly string[] Editors =
    [
        ApplicationRoles.Actions.Edit,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer
    ];

    public static readonly string[] Deleters =
    [
        ApplicationRoles.Actions.Delete,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer
    ];

    public static class Modules
    {
        public static readonly string[] Warehouse =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Warehouse.KHOUser
        ];

        public static readonly string[] Lab =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser
        ];

        public static readonly string[] Manufacturing =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.ManufactureUser,
            ApplicationRoles.Production.QLSXUser
        ];

        public static readonly string[] Sales =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.SaleUser
        ];

        public static readonly string[] Purchasing =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Purchasing.Purchaser,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] Accounting =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser
        ];

        public static readonly string[] PLPU =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser,
        ];
    }

    public static class Notifications
    {
        public static readonly string[] SampleRequestSalesAdmins = [ApplicationRoles.Sales.SaleAdmin];
        public static readonly string[] SampleRequestLabAdmins = [ApplicationRoles.Lab.LabAdmin];
        public static readonly string[] SampleRequestPriceReviewers = [ApplicationRoles.President];
        public static readonly string[] ManufacturingPriceWarningRecipients =
        [ApplicationRoles.Notifications.PLPUNotify, ApplicationRoles.President, ApplicationRoles.Developer];
        public const string BackfillManagerRolesCsv =
            $"{ApplicationRoles.Admin},{ApplicationRoles.Developer}";

        public static readonly string[] BackfillManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer
        ];

        public static readonly string[] RecipientManagers =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ConversationParticipantManagers =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];
    }

    public static class CRM
    {
        public const string DirectorReaderRolesCsv =
            $"{ApplicationRoles.President},{ApplicationRoles.Developer},{ApplicationRoles.Admin},{ApplicationRoles.Accounting.ACCUser}";

        public const string LeaderReaderRolesCsv =
            $"{ApplicationRoles.President},{ApplicationRoles.Developer},{ApplicationRoles.Leader},{ApplicationRoles.Accounting.ACCUser}";

        public static readonly string[] ExecutiveReaders =
        [ApplicationRoles.President, ApplicationRoles.Accounting.ACCUser];

        public static readonly string[] CustomerEditors =
        [
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];
    }

    public static class Pricing
    {
        public static readonly string[] ExecutiveReaders =
        [ApplicationRoles.Developer, ApplicationRoles.President, ApplicationRoles.Accounting.ACCUser];

        public static readonly string[] WorkbenchViewers =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Accounting.ACCUser
        ];

        public static readonly string[] ApprovedSellingPriceViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Sales.PriceView,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] SystemCalculatedPriceViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.PriceView,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] MaterialCostViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] ManufacturingCostViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser
        ];

        public static readonly string[] MarginViewers = ManufacturingCostViewers;

        public static readonly string[] HistoryViewers = ManufacturingCostViewers;

        public static readonly string[] Managers =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser
        ];

        public static readonly string[] Approvers = Managers;
    }

    public static class Dispatch
    {
        // Giữ nguyên ma trận quyền cũ khi tách delivery cost khỏi PLM formula price.
        public static readonly string[] DeliveryCostViewers = PLM.FormulaPriceViewers;
    }

    public static class EmployeeAdministration
    {
        public static readonly string[] EmployeeViewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser];

        public static readonly string[] EmployeeManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] GlobalCompanyManagers =
        [
            ApplicationRoles.Developer
        ];

        public static readonly string[] RoleTypeManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer
        ];

        public static readonly string[] PrivilegedRoles =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];
    }

    public static class PLM
    {
        public static readonly string[] LabStandardReceivers =
            [ApplicationRoles.Lab.LabAdmin, ApplicationRoles.Developer, ApplicationRoles.President, ApplicationRoles.Admin];
        public static readonly string[] SampleRequestDeactivators = [ApplicationRoles.Sales.SaleUser];
        public static readonly string[] SampleTrialFeedbackCorrectors = [ApplicationRoles.Sales.SaleUser, ApplicationRoles.Sales.SaleAdmin];
        public static readonly string[] ManufacturingDefectiveLotViewers = [ApplicationRoles.Developer, ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
        public static readonly string[] ManufacturingAdjustmentExcludedRoles = [ApplicationRoles.Production.PLPUUser];
        public static readonly string[] ManufacturingAdjustmentEditors =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
        // ColorChip (/manufacturing-color-chips) giữ nguyên quyền hiện có, độc lập phiếu chỉnh màu.
        public static readonly string[] ManufacturingColorChipEditors =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser, ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Quality.QCUser, ApplicationRoles.Lab.LabUser];
        // Legacy formula editor allows LabUser and Developer to edit laboratory data.
        public static readonly string[] ManufacturingFormulaEditors = [ApplicationRoles.Lab.LabUser, ApplicationRoles.Developer];
        public static readonly string[] ProductionOrderViewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.Production.PLPUUser, ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Production.ManufactureUser, ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin, ApplicationRoles.Quality.QCUser];

        public static readonly string[] ProductionOrderCheckers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser, ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Quality.QCUser];

        public static readonly string[] ProductionOrderRequirementEditors = [ApplicationRoles.Developer];

        public static readonly string[] ProductionOrderManagers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser, ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Production.ManufactureUser];

        public static readonly string[] ProductionOrderCreators =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser, ApplicationRoles.Production.QLSXUser,
            ApplicationRoles.Production.ManufactureUser];
        public static readonly string[] SampleRequestPriceQuoteRequesters =
        [
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Leader,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ComplaintCreators =
        [
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ComplaintInvestigators =
        [
            ApplicationRoles.Quality.QCUser,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ComplaintApprovers =
        [
            ApplicationRoles.Leader,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ComplaintVerifiers =
        [
            ApplicationRoles.Quality.QCUser,
            ApplicationRoles.Leader,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ComplaintPdfViewers =
        [
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Quality.QCUser,
            ApplicationRoles.Leader,
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser
        ];

        public static readonly string[] SaleOrderApprovers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Leader
        ];

        public static readonly string[] StandardSampleConfirmers =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Leader,
            ApplicationRoles.Sales.SaleAdmin
        ];

        public static readonly string[] FormulaMaterialViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] FormulaPriceViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.PriceView,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] MaterialDocumentExtractors =
            [.. SuperUsers, ApplicationRoles.Production.PLPUUser];

        public static readonly string[] MaterialSupplierPriceEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Purchasing.Purchaser,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] MaterialPriceReviewViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] MaterialPriceReviewDetailViewers =
        [.. MaterialSupplierPriceEditors, ApplicationRoles.Accounting.ACCUser];

        public static readonly string[] MaterialPurchaseAvailabilityManagers = MaterialSupplierPriceEditors;

        public static readonly string[] MaterialReplacementViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] MaterialReplacementManagers = MaterialSupplierPriceEditors;

        public static readonly string[] MaterialAvailabilityLabRecipients =
        [
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin,
            ApplicationRoles.Developer,
            ApplicationRoles.Purchasing.Purchaser,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] FormulaManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin
        ];

        public static readonly string[] BomViewers = FormulaMaterialViewers;

        public static readonly string[] BomDraftManagers = FormulaManagers;

        public static readonly string[] BomReleasers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Leader,
            ApplicationRoles.Lab.LabAdmin,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] BomStandardAssigners =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Leader,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] BomLossManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] FormulaPricingEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] ProductionInformOperators =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] ProductUsageAndExpiryEditors =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser
        ];

        public static readonly string[] ProductTechnicalEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] ProductTechnicalViewers =
        [.. ProductTechnicalEditors, ApplicationRoles.Accounting.ACCUser];

        public static readonly string[] FormulaSelectors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Leader
        ];

        public static readonly string[] SampleSaleWorkflowEditors =
            [.. FormulaSelectors, ApplicationRoles.Sales.SaleAdmin];

        public static readonly string[] ManagedSampleSaleWorkflowEditors =
            [ApplicationRoles.Sales.SaleAdmin];

        public static readonly string[] SampleRequestExpectedPriceQuoteDateEditors =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser
        ];

        public static readonly string[] SampleRequestLabProgressEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] SampleRequestLabWorkflowEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

        public static readonly string[] SampleWorkScheduleManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.Lab.LabAdmin
        ];

    }
}
