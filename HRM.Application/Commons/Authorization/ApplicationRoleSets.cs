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
            ApplicationRoles.Accounting.ACUser
        ];

        public static readonly string[] PLPU =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser,
        ];
    }

    public static class PLM
    {
        public static readonly string[] FormulaMaterialViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Production.PLPUUser,
            ApplicationRoles.Accounting.ACUser,
            ApplicationRoles.Lab.LabUser
        ];

        public static readonly string[] FormulaPriceViewers =
        [
            ApplicationRoles.Admin,
            ApplicationRoles.Developer,
            ApplicationRoles.President,
            ApplicationRoles.Sales.PriceView,
            ApplicationRoles.Accounting.ACUser,
            ApplicationRoles.SeePrice.SeePriceUser
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
