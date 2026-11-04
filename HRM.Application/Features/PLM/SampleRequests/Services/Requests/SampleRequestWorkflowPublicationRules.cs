using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestWorkflowPublicationRules
{
    // Legacy operational notices remain; tracking-only events must not introduce new notices.
    public static bool ShouldDispatch(DateTime requestCreatedAt, string eventType)
        => !Services.Workflow.SampleFlowRules.IsLegacy(requestCreatedAt) ||
            eventType is SampleRequestWorkflowEvents.Created or SampleRequestWorkflowEvents.FormulaConfirmed or
                SampleRequestWorkflowEvents.SampleSent or SampleRequestWorkflowEvents.RequestCancelled;

    // A recorded business action may publish its system notice even when its actor cannot
    // chat in General. This never grants conversation membership or changes recipient scope.
    public static bool IsTrustedPublication(SampleRequestWorkflowEvent? evt, Guid requestId,
        Guid companyId, Guid actorId, bool actorIsActive)
        => evt is not null && evt.Id != Guid.Empty && actorId != Guid.Empty && companyId != Guid.Empty &&
            requestId != Guid.Empty && actorIsActive && evt.SampleRequestId == requestId &&
            evt.CompanyId == companyId && evt.RecordedByEmployeeId == actorId &&
            evt.EventType is SampleRequestWorkflowEvents.Created or SampleRequestWorkflowEvents.StandardReceived or
                SampleRequestWorkflowEvents.FormulaConfirmed or SampleRequestWorkflowEvents.PriceAccepted or
                SampleRequestWorkflowEvents.PriceRevoked or SampleRequestWorkflowEvents.SampleSent or
                SampleRequestWorkflowEvents.FeedbackRecorded or SampleRequestWorkflowEvents.RequestCancelled or
                Services.Workflow.SampleFlowRules.PriceFeedbackShared or Services.Workflow.SampleFlowRules.QuotationFinalized or Services.Workflow.SampleFlowRules.ChipFinalized or Services.Workflow.SampleFlowRules.ChipReopened or Services.Workflow.SampleFlowRules.KgRequested or
                Services.Workflow.SampleFlowRules.CompletedWithoutTrial or Services.Workflow.SampleFlowRules.PriceRejected or
                Services.Workflow.SampleFlowRules.PriceNoResponse;
}
