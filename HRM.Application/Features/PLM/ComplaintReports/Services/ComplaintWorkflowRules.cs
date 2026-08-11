using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

public static class ComplaintWorkflowRules
{
    private const ComplaintRelatedStandard AllStandards =
        ComplaintRelatedStandard.Quality |
        ComplaintRelatedStandard.GlobalRecycledStandard |
        ComplaintRelatedStandard.HealthAndSafety |
        ComplaintRelatedStandard.Environment |
        ComplaintRelatedStandard.Other;

    private const ComplaintRelatedScope AllScopes =
        ComplaintRelatedScope.CustomerClaim |
        ComplaintRelatedScope.InternalAudit |
        ComplaintRelatedScope.ProductionControl;

    public static bool CanEditInvestigation(ComplaintReportStatus status)
        => status is ComplaintReportStatus.Investigating or ComplaintReportStatus.ActionInProgress;

    public static bool CanReplaceActions(ComplaintReportStatus status)
        => status is ComplaintReportStatus.Investigating or ComplaintReportStatus.ActionInProgress;

    public static bool CanUpdateActionResult(ComplaintReportStatus status)
        => status == ComplaintReportStatus.ActionInProgress;

    public static bool CanRequestVerification(ComplaintReportStatus status)
        => status == ComplaintReportStatus.ActionInProgress;

    public static bool CanSaveEffectiveness(ComplaintReportStatus status)
        => status == ComplaintReportStatus.PendingVerification;

    public static bool AreStandardsValid(ComplaintRelatedStandard value)
        => (value & ~AllStandards) == 0;

    public static bool AreScopesValid(ComplaintRelatedScope value)
        => value != ComplaintRelatedScope.None && (value & ~AllScopes) == 0;
}
