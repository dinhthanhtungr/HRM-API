using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

public static class DeliveryOrderAccessRules
{
    public const string ReadRoles =
        "DispatchUser,PLPUUser,SaleUser,KHOUser,ACCUser,Leader,Edit,Delete,Admin,Developer,President";

    public const string ManageRoles =
        "DispatchUser,PLPUUser,Edit,Admin,Developer,President";
    public const string FinishRoles = ManageRoles + ",SaleUser";

    public static bool CanFinish(ICurrentUser currentUser)
        => CanManage(currentUser) || currentUser.IsAuthenticated && currentUser.IsInRole(ApplicationRoles.Sales.SaleUser);

    public const string CancelRoles =
        "DispatchUser,PLPUUser,Delete,Admin,Developer,President";

    private static readonly string[] Readers =
    [
        ApplicationRoles.Dispatch.DispatchUser,
        ApplicationRoles.Production.PLPUUser,
        ApplicationRoles.Sales.SaleUser,
        ApplicationRoles.Warehouse.KHOUser,
        ApplicationRoles.Accounting.ACCUser,
        ApplicationRoles.Leader,
        ApplicationRoles.Actions.Edit,
        ApplicationRoles.Actions.Delete,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    private static readonly string[] Managers =
    [
        ApplicationRoles.Dispatch.DispatchUser,
        ApplicationRoles.Production.PLPUUser,
        ApplicationRoles.Actions.Edit,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    private static readonly string[] Cancellers =
    [
        ApplicationRoles.Dispatch.DispatchUser,
        ApplicationRoles.Production.PLPUUser,
        ApplicationRoles.Actions.Delete,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    public static bool CanRead(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Readers);

    // Sale/Leader đọc kế hoạch theo khách hàng được phân công; role điều vận giữ phạm vi công ty.
    private static readonly string[] CompanyPlanReaders = Readers
        .Except([ApplicationRoles.Sales.SaleUser, ApplicationRoles.Leader]).ToArray();

    public static bool RequiresCustomerScopeForPlan(ICurrentUser currentUser)
        => CanRead(currentUser) && !currentUser.IsInAnyRole(CompanyPlanReaders);

    public static bool CanManage(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Managers);

    public static bool CanCancel(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Cancellers);
}
