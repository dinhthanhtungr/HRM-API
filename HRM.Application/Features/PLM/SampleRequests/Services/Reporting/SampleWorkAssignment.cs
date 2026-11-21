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
        var open = item.SampleRequestStatus is not "Completed" and not "Cancelled" and not "LabDeclined";
        var technical = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow);
        var mine = decision.Role switch
        {
            "Lab" => technical && audience != SampleRequestWorkAudience.Sale,
            "Sale" => permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
                (item.CanActForSaleOwner ?? user.EmployeeId == item.ManagerSalesEmployeeId) &&
                audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin),
            "President" => user.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
            // Quyền chỉnh lịch không tự nhận việc rà soát của màn quản lý trong màn Lab/Sale.
            "LabAdmin" => permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule) &&
                audience is null or SampleRequestWorkAudience.LabAdmin,
            _ => false
        };
        item.CanEditLabSchedule = open && permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule);
        item.PrimaryActionCode = open && mine ? decision.Code : null;
        if (audience == SampleRequestWorkAudience.LabAdmin && !mine)
        {
            item.PrimaryActionCode = item.CanEditLabSchedule
                ? decision.Code == "ReviewSample" ? "ReviewSample" : "ScheduleLabWork"
                : null;
            mine = item.PrimaryActionCode != null;
        }
        item.IsActionForCurrentUser = open && mine;
        if (open && decision.Code == "ResumeAfterLabRejection" && item.Flow?.AllowedActions.Contains("ResumeAfterLabRejection") == true && permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
            audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin))
        {
            item.PrimaryActionCode = decision.Code;
            item.IsActionForCurrentUser = true;
        }
        if (open && audience == SampleRequestWorkAudience.Sale && item.Flow?.AllowedActions.Contains("RequestChip") == true)
        {
            item.PrimaryActionCode = "ChooseAfterPrice";
            item.IsActionForCurrentUser = true;
        }
        item.IsLabWorkComplete = decision.Role != "Lab" && decision.Stage != "LabRejected";
        if (decision.Stage == "LabRejected")
        {
            item.CanEditLabSchedule = false;
            item.CanReceiveLabStandard = false;
            item.CanWriteLabNote = false;
            if (audience is SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin)
            {
                item.PrimaryActionCode = null;
                item.IsActionForCurrentUser = false;
            }
        }
        item.SupplementaryActionCodes = !open ? [] :
            (item.CanEditLabSchedule ? new[] { "EditLabSchedule" } : [])
            .Concat(item.CanReceiveLabStandard && technical ? new[] { "ReceiveStandard" } : [])
            .Concat(item.Flow?.AllowedActions.Contains("ReopenChip") == true && technical ? new[] { "ReopenChip" } : []).ToArray();
        if (item.Flow is { } flow && !technical && audience == SampleRequestWorkAudience.LabAdmin)
            item.Flow = flow with { AllowedActions = [] };
        var pending = new List<string>();
        var feedbackRecorded = item.HasCurrentDeliveryFeedback ?? item.CustomerReplyDate.HasValue;
        if (item.PrimaryActionCode is { } primary) pending.Add(primary);
        var ownsSaleWork = permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
            (item.CanActForSaleOwner ?? user.EmployeeId == item.ManagerSalesEmployeeId) &&
            audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin);
        if (open && item.Flow?.IsLabPaused != true && ownsSaleWork)
        {
            if (item.CanConfirmCustomerPrice || item.Flow?.AllowedActions.Any(a => a is "PriceRejected" or "PriceNoResponse") == true)
                pending.Add(item.RealPriceQuoteDate.HasValue ? "PriceFeedback" : "SendQuotation");
            if (item.CanUpdateCustomerFeedback && !feedbackRecorded) pending.Add("FollowUpCustomer");
            if (feedbackRecorded) item.SupplementaryActionCodes = item.SupplementaryActionCodes.Append("ViewCustomerFeedback").ToArray();
            // Buying is an explicit Sale decision, not unfinished work merely because it is allowed.
        }
        item.PendingActionCodes = pending.Distinct().Where(code => (code != "FollowUpCustomer" || !feedbackRecorded) && code is not
            ("ChooseAfterPrice" or "ChooseAfterChip" or "TrackApprovedChip" or "TrackRequest" or "CompleteRequest"))
            .OrderBy(code => code is "PriceFeedback" or "SendQuotation" or "ConfirmCustomerPrice" ? 0 :
                code == "FollowUpCustomer" ? 1 : code == "SendSample" ? 2 : 3).ToArray();
        if (item.PendingActionCodes.Count > 0 && ownsSaleWork && item.Flow?.IsLabPaused != true)
        {
            item.PrimaryActionCode = item.PendingActionCodes[0];
            item.IsActionForCurrentUser = true;
        }
        else if (feedbackRecorded && item.PrimaryActionCode == "FollowUpCustomer")
        {
            item.PrimaryActionCode = null;
            item.IsActionForCurrentUser = false;
        }
    }
}
