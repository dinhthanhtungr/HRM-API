using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal static class SampleWorkAssignment
{
    internal static void Apply(SampleRequestSampleTrialReportDto item, ICurrentUser user,
        ICurrentUserPermissionService permissions, SampleRequestWorkAudience? audience)
    {
        if (item.WorkDecision is not { } decision) return;
        item.NextActionCode = decision.Code;
        item.NextActionLabel = null;
        item.ResponsibleRole = decision.Role;
        item.ResponsibleEmployeeId = decision.Role == "Sale" ? item.ManagerSalesEmployeeId : null;
        item.WorkStatus = decision.Stage;
        var open = item.SampleRequestStatus is not "Completed" and not "Cancelled";
        var technical = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow);
        var mine = decision.Role switch
        {
            "Lab" => technical && audience != SampleRequestWorkAudience.Sale && audience != SampleRequestWorkAudience.LabAdmin,
            "Sale" => permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
                (item.CanActForSaleOwner ?? user.EmployeeId == item.ManagerSalesEmployeeId) &&
                audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin),
            "President" => user.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
            "LabAdmin" => permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule),
            _ => false
        };
        item.CanEditLabSchedule = open && permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule);
        item.PrimaryActionCode = open && mine ? decision.Code : null;
        if (audience == SampleRequestWorkAudience.LabAdmin)
        {
            item.PrimaryActionCode = item.CanEditLabSchedule && item.NeedsLabSchedule ? "ScheduleLabWork" :
                decision.Code == "ReviewSample" && item.CanEditLabSchedule ? "ReviewSample" : null;
            mine = item.PrimaryActionCode != null;
        }
        item.IsActionForCurrentUser = open && mine;
        item.IsLabWorkComplete = decision.Role != "Lab";
        item.SupplementaryActionCodes = !open ? [] :
            (item.CanEditLabSchedule ? new[] { "EditLabSchedule" } : [])
            .Concat(item.CanReceiveLabStandard && technical ? new[] { "ReceiveStandard" } : [])
            .Concat(item.Flow?.AllowedActions.Contains("ReopenChip") == true && technical ? new[] { "ReopenChip" } : []).ToArray();
        if (item.Flow is { } flow && !technical && audience == SampleRequestWorkAudience.LabAdmin)
            item.Flow = flow with { AllowedActions = [] };
    }
}
