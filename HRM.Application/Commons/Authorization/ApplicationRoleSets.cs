namespace HRM.Application.Commons.Authorization;

public static class ApplicationRoleSets
{
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
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Accounting.ACCUser,
            ApplicationRoles.Lab.LabUser
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
            ApplicationRoles.Purchasing.Purchaser
        ];

        public static readonly string[] FormulaManagers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Lab.LabUser,
            ApplicationRoles.Lab.LabAdmin
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
    }
}
