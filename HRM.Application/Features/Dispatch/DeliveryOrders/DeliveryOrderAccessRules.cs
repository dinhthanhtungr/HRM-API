using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

public static class DeliveryOrderAccessRules
{
    public const string ReadRoles =
        "DispatchUser,SaleUser,KHOUser,ACCUser,Leader,Edit,Delete,Admin,Developer,President";

    public const string ManageRoles =
        "DispatchUser,Edit,Admin,Developer,President";

    public const string CancelRoles =
        "DispatchUser,Delete,Admin,Developer,President";

    private static readonly string[] Readers =
    [
        ApplicationRoles.Dispatch.DispatchUser,
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
        ApplicationRoles.Actions.Edit,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    private static readonly string[] Cancellers =
    [
        ApplicationRoles.Dispatch.DispatchUser,
        ApplicationRoles.Actions.Delete,
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    public static bool CanRead(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Readers);

    public static bool CanManage(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Managers);

    public static bool CanCancel(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(Cancellers);
}
