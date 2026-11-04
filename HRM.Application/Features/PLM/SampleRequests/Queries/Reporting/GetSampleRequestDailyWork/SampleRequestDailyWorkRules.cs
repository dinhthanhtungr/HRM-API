using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestDailyWorkRules
{
    public static bool IsQuotation(string? infoType) =>
        HRM.Application.Features.PLM.SampleRequests.Rules.SampleRequestCustomerPriceRules.IsQuotation(infoType);

    public static void Apply(
        SampleRequestSampleTrialReportDto item,
        DateOnly date,
        bool hasApprovedPriceForFormula,
        bool canHandleLab,
        bool canApprovePrice,
        bool canHandleSale,
        Guid? currentEmployeeId,
        bool useDelayReviewAction = true)
    {
        if (item.Flow is { } flow)
        {
            ApplyFlow(item, flow, date, hasApprovedPriceForFormula, canHandleLab, canApprovePrice, canHandleSale, currentEmployeeId);
            return;
        }
        var (dueDate, dueDateSource) = ResolveDueDate(item);
        item.IsLabWorkComplete = IsLabComplete(item);
        item.LabCompletedAt = item.IsLabWorkComplete == true
            ? IsQuotation(item.InfoType) ? item.LabFormulaConfirmedAt : item.SentDate
            : null;
        var feedbackToday = item.CustomerReplyDate.HasValue &&
            DateOnly.FromDateTime(item.CustomerReplyDate.Value) == date;
        var (status, action, label, role) = ResolveAction(
            item, hasApprovedPriceForFormula, feedbackToday, useDelayReviewAction);
        // A manual pin can surface an open request whose normal Lab/Sale work has ended.
        // Keep real pending actions; only supply a review action when there is otherwise none.
        if (!useDelayReviewAction && item.IsDelayed && string.IsNullOrEmpty(action) &&
            item.SampleRequestStatus is not "Completed" and not "Cancelled")
            (status, action, label, role) = ("ReviewDelayedWork", "ReviewSample", "Lab kiểm tra hồ sơ đang treo", "Lab");

        item.WorkStatus = status;
        item.NextActionCode = action;
        item.NextActionLabel = label;
        item.ResponsibleRole = role;
        item.ResponsibleEmployeeId = role == "Sale" ? item.ManagerSalesEmployeeId : null;
        item.IsActionForCurrentUser = !string.IsNullOrEmpty(action) && (role switch
        {
            "Lab" => canHandleLab,
            "President" => canApprovePrice,
            "Sale" => canHandleSale && currentEmployeeId.HasValue &&
                currentEmployeeId.Value == item.ManagerSalesEmployeeId,
            _ => false
        });
        item.DueDate = dueDate;
        item.DueDateSource = dueDateSource;
        item.IsOverdue = dueDate.HasValue && DateOnly.FromDateTime(dueDate.Value) < date &&
            item.IsLabWorkComplete != true;
        item.DelayReason = item.IsDelayed
            ? item.IsOverdue == true ? "ManualAndOverdue" : "Manual"
            : item.IsOverdue == true ? "Overdue" : "None";
        item.FeedbackRecordedOnDate = feedbackToday;
    }

    private static void ApplyFlow(SampleRequestSampleTrialReportDto item, Dtos.Reports.SampleFlowStateDto flow,
        DateOnly date, bool hasApprovedPrice, bool lab, bool approvePrice, bool sale, Guid? employee)
    {
        var (action, role) = flow.Stage switch
        {
            "LegacyPreparation" or "PrepareChip" or "PrepareSample" => ("ApproveFormula", "Lab"),
            "ReworkChip" => ("ReworkFormula", "Lab"),
            "FinalizeChipFormula" => ("FinalizeChip", "Lab"),
            "ReadyToSendSample" => ("SendSample", "Lab"),
            "AwaitingChipFeedback" or "AwaitingSampleFeedback" => ("FollowUpCustomer", "Sale"),
            "AwaitingPriceFeedback" => item.RealPriceQuoteDate.HasValue ? ("PriceFeedback", "Sale") : ("SendQuotation", "Sale"),
            "AwaitingStandardPrice" => ("ApproveStandardPrice", "President"),
            "AwaitingSaleDecision" or "LegacySent" or "QuotationAccepted" => ("CompleteRequest", "Sale"),
            "QuotationPreparation" => !item.LabFormulaConfirmedAt.HasValue ? ("ApproveFormula", "Lab") :
                !hasApprovedPrice ? ("ApproveStandardPrice", "President") : ("SendQuotation", "Sale"),
            _ => (string.Empty, string.Empty)
        };
        if (flow.Stage is "PrepareChip" or "ReworkChip" && flow.AllowedActions.Contains("Send")) action = "SendSample";
        item.WorkStatus = flow.Stage;
        item.NextActionCode = action;
        item.NextActionLabel = null;
        item.ResponsibleRole = role;
        item.ResponsibleEmployeeId = role == "Sale" ? item.ManagerSalesEmployeeId : null;
        item.IsActionForCurrentUser = action.Length > 0 && (role switch
        {
            "Lab" => lab,
            "President" => approvePrice,
            "Sale" => sale && employee.HasValue && employee == item.ManagerSalesEmployeeId,
            _ => false
        });
        item.IsLabWorkComplete = role != "Lab";
        var (due, source) = ResolveDueDate(item);
        item.DueDate = due;
        item.DueDateSource = source;
        item.IsOverdue = due.HasValue && DateOnly.FromDateTime(due.Value) < date && role == "Lab";
        item.FeedbackRecordedOnDate = item.CustomerReplyDate.HasValue && DateOnly.FromDateTime(item.CustomerReplyDate.Value) == date;
    }

    private static (string Status, string Action, string Label, string Role) ResolveAction(
        SampleRequestSampleTrialReportDto item,
        bool hasApprovedPrice,
        bool feedbackToday,
        bool useDelayReviewAction)
    {
        if (useDelayReviewAction && item.IsLabWorkComplete == true && item.IsDelayed &&
            item.SampleRequestStatus is not "Completed" and not "Cancelled")
            return ("ReviewDelayFlag", "ClearDelay", "Lab kiểm tra hồ sơ được đánh dấu delay", "Lab");
        if (item.Status is SampleTrialStatus.Approved or SampleTrialStatus.Cancelled ||
            item.SampleRequestStatus is "Completed" or "Cancelled")
        {
            return feedbackToday
                ? ("FeedbackRecorded", string.Empty, "Đã ghi nhận phản hồi", string.Empty)
                : ("Closed", string.Empty, "Đã kết thúc", string.Empty);
        }
        if (item.Status == SampleTrialStatus.Failed && item.IsLabWorkComplete != true)
            return ("ReworkRequired", "ReworkFormula", "Lab làm lại công thức", "Lab");
        if (item.Status == SampleTrialStatus.SampleSent)
            return ("AwaitingCustomerFeedback", "FollowUpCustomer", "Sale theo dõi phản hồi khách", "Sale");
        if (item.Status == SampleTrialStatus.WaitingCustomerFeedback)
            return ("AwaitingCustomerFeedback", "FollowUpCustomer", "Sale gửi khách hoặc theo dõi phản hồi", "Sale");
        if (item.Status == SampleTrialStatus.PriceQuote)
            return ("AwaitingQuotation", "SendQuotation", "Sale theo dõi báo giá cho khách", "Sale");

        var formulaApproved = item.FormulaStatus is "Approved" or "SampleSent" or "Completed" ||
            item.LabFormulaConfirmedAt.HasValue;
        if (IsQuotation(item.InfoType))
        {
            if (item.IsLabWorkComplete != true)
                return ("AwaitingFormulaApproval", "ApproveFormula", "Lab xác nhận công thức để Ban giám đốc báo giá", "Lab");
            if (!hasApprovedPrice)
                return ("AwaitingStandardPrice", "ApproveStandardPrice", "Duyệt giá chuẩn để báo giá", "President");
            if (!item.RealPriceQuoteDate.HasValue)
                return ("AwaitingQuotation", "SendQuotation", "Sale báo giá cho khách", "Sale");
            return ("Closed", string.Empty, "Đã báo giá", string.Empty);
        }
        return formulaApproved
            ? ("AwaitingLabSend", "SendSample", "Lab gửi mẫu cho Sale", "Lab")
            : ("AwaitingFormulaApproval", "ApproveFormula", "Lab hoàn tất và duyệt công thức", "Lab");
    }

    internal static bool IsLabComplete(SampleRequestSampleTrialReportDto item)
    {
        if (item.SampleRequestStatus is "Completed" or "Cancelled" ||
            item.Status is SampleTrialStatus.Approved or SampleTrialStatus.Cancelled)
            return true;
        if (IsQuotation(item.InfoType))
        {
            if (item.LabFormulaConfirmedAt is not { } confirmedAt)
                return false;
            if (item.Status == SampleTrialStatus.Failed)
                return confirmedAt > (item.CustomerReplyDate ?? item.UpdatedDate ?? item.CreatedDate);
            if (item.Status == SampleTrialStatus.Draft && item.HasTrial)
                return confirmedAt >= item.CreatedDate;
            return true;
        }
        if (item.Status is SampleTrialStatus.Failed or SampleTrialStatus.Draft)
            return false;
        return item.Status is SampleTrialStatus.SampleSent or SampleTrialStatus.WaitingCustomerFeedback ||
            item.SampleRequestStatus == "SampleSent";
    }

    internal static (DateTime? Date, string Source) ResolveDueDate(SampleRequestSampleTrialReportDto item)
    {
        if (IsQuotation(item.InfoType) && item.RequestedSampleQuantity is not > 0)
            return (item.ExpectedPriceQuoteDate, item.ExpectedPriceQuoteDate.HasValue ? "PriceQuoteExpected" : "Unknown");
        if (item.ExpectedDeliveryDate.HasValue)
            return (item.ExpectedDeliveryDate, "LabExpected");
        if (item.RequestDeliveryDate.HasValue)
            return (item.RequestDeliveryDate, "SaleRequested");
        if (item.LabReceivedDate is { } received && item.NumberDeliverySampleDate is >= 0 and var days &&
            days <= (DateTime.MaxValue.Date - received.Date).TotalDays)
            return (received.Date.AddDays(days), "LabReceiptPlusDays");
        return (null, "Unknown");
    }
}
