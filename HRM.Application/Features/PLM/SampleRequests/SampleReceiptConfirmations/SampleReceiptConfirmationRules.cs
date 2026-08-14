using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;

internal static class SampleReceiptConfirmationRules
{
    private static readonly string[] ConfirmerRoles =
    [
        ApplicationRoles.Sales.SaleUser,
        ApplicationRoles.Developer,
        ApplicationRoles.President
    ];

    private static readonly TimeSpan FutureClockTolerance = TimeSpan.FromMinutes(5);

    public static bool CanConfirm(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ConfirmerRoles);

    public static DateTime ResolveReceivedDate(DateTime? requestedDate, DateTime now)
        => requestedDate ?? now;

    public static string? Validate(
        SampleTrialStatus trialStatus,
        DateTime receivedDate,
        DateTime now)
    {
        if (trialStatus is not SampleTrialStatus.SampleSent and
            not SampleTrialStatus.WaitingCustomerFeedback)
        {
            return "Only a sent sample awaiting customer feedback can be confirmed as received.";
        }

        if (receivedDate > now.Add(FutureClockTolerance))
        {
            return "SampleReceivedDate cannot be in the future.";
        }

        return null;
    }
}
