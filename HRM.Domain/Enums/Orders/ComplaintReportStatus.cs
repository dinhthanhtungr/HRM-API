namespace HRM.Domain.Enums.Orders;

public enum ComplaintReportStatus
{
    Draft = 0,
    Submitted = 1,
    Investigating = 2,
    Closed = 3,
    Rejected = 4,
    Cancelled = 5,
    ActionInProgress = 6,
    PendingVerification = 7,
    PendingFinalApproval = 8
}
