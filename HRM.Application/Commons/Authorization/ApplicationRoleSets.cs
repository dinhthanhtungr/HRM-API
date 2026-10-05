namespace HRM.Application.Commons.Authorization;

public static class ApplicationRoleSets
{
    public static class InternalMail
    {
        public static readonly string[] MessageDeleters = [ApplicationRoles.Developer];
    }

    public static class Equipment
    {
        public static readonly string[] Viewers =
        [ApplicationRoles.Admin, ApplicationRoles.Developer, ApplicationRoles.President,
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
            ApplicationRoles.Purchasing.Purchaser
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
            ApplicationRoles.President
        ];

        public static readonly string[] SaleOrderApprovers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Leader
        ];

        public static readonly string[] FormulaMaterialViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
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
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.SeePrice.SeePriceUser
        ];

        public static readonly string[] MaterialPriceReviewDetailViewers = MaterialSupplierPriceEditors;

        public static readonly string[] MaterialPurchaseAvailabilityManagers = MaterialSupplierPriceEditors;

        public static readonly string[] MaterialReplacementViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
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

        public static readonly string[] ProductTechnicalEditors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Production.PLPUUser
        ];

        public static readonly string[] FormulaSelectors =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.SaleUser,
            ApplicationRoles.Leader
        ];

        public static readonly string[] SampleRequestExpectedPriceQuoteDateEditors =
        [
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser
        ];

        public static readonly string[] SampleRequestLabProgressEditors =
        [
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin,
            ApplicationRoles.Developer,
            ApplicationRoles.President
        ];

    }
}
