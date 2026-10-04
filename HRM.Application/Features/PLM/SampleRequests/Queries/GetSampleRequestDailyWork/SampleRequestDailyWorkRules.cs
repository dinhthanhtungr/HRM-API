using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestDailyWorkRules
{
    public static bool IsQuotation(string? infoType) =>
        string.Equals(infoType?.Trim(), "Quotation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(infoType?.Trim(), "Báo giá", StringComparison.OrdinalIgnoreCase);

    public static void Apply(
        SampleRequestSampleTrialReportDto item,
        DateOnly date,
        bool hasApprovedPriceForFormula,
        bool canHandleLab,
        bool canApprovePrice,
        bool canHandleSale,
        Guid? currentEmployeeId)
    {
        var dueDate = IsQuotation(item.InfoType)
            ? item.ExpectedPriceQuoteDate
            : item.ExpectedDeliveryDate;
        var feedbackToday = item.CustomerReplyDate.HasValue &&
            DateOnly.FromDateTime(item.CustomerReplyDate.Value) == date;
        var (status, action, label, role) = ResolveAction(
            item, hasApprovedPriceForFormula, feedbackToday);

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
        item.IsOverdue = dueDate.HasValue && DateOnly.FromDateTime(dueDate.Value) < date &&
            !string.IsNullOrEmpty(action);
        item.FeedbackRecordedOnDate = feedbackToday;
    }

    private static (string Status, string Action, string Label, string Role) ResolveAction(
        SampleRequestSampleTrialReportDto item,
        bool hasApprovedPrice,
        bool feedbackToday)
    {
        if (item.Status is SampleTrialStatus.Approved or SampleTrialStatus.Cancelled ||
            item.SampleRequestStatus is "Completed" or "Cancelled")
        {
            if (item.IsDelayed)
                return ("ReviewDelayFlag", "ClearDelay", "Kiểm tra và bỏ cờ delay của hồ sơ đã kết thúc", "Lab");
            return feedbackToday
                ? ("FeedbackRecorded", string.Empty, "Đã ghi nhận phản hồi", string.Empty)
                : ("Closed", string.Empty, "Đã kết thúc", string.Empty);
        }
        if (item.Status == SampleTrialStatus.Failed)
            return ("ReworkRequired", "ReworkFormula", "Lab làm lại công thức", "Lab");
        if (item.Status == SampleTrialStatus.SampleSent)
            return item.RequestReceivedDate.HasValue
                ? ("AwaitingCustomerFeedback", "FollowUpCustomer", "Sale gửi khách hoặc theo dõi phản hồi", "Sale")
                : ("AwaitingSaleReceipt", "HandleLabSample", "Sale xử lý mẫu Lab vừa gửi", "Sale");
        if (item.Status == SampleTrialStatus.WaitingCustomerFeedback)
            return ("AwaitingCustomerFeedback", "FollowUpCustomer", "Sale gửi khách hoặc theo dõi phản hồi", "Sale");
        if (item.LabReceivedDate is null)
            return ("AwaitingLabReceipt", "RecordLabReceipt", "Lab ghi nhận đã nhận mẫu từ Sale", "Lab");

        var formulaApproved = item.FormulaStatus is "Approved" or "SampleSent" or "Completed";
        if (IsQuotation(item.InfoType))
        {
            if (!formulaApproved)
                return ("AwaitingFormulaApproval", "ApproveFormula", "Lab hoàn tất và duyệt công thức", "Lab");
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
}
