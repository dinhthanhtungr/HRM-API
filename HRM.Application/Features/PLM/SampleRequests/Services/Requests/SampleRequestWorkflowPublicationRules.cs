using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestWorkflowPublicationRules
{
    // Outcome history is always persisted. Pricing chat requires an explicit private note.
    // Previously published messages retain their original retry/visibility handling.
    internal static bool ShouldPublishMessage(SampleRequestWorkflowEvent evt)
        => !SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType) ||
            !string.IsNullOrWhiteSpace(evt.Note) || evt.MessageId.HasValue;

    // Legacy operational notices remain; tracking-only events must not introduce new notices.
    public static bool ShouldDispatch(DateTime requestCreatedAt, string eventType)
        => !Services.Workflow.SampleFlowRules.IsLegacy(requestCreatedAt) ||
            eventType is SampleRequestWorkflowEvents.Created or SampleRequestWorkflowEvents.FormulaConfirmed or
                SampleRequestWorkflowEvents.SampleSent or SampleRequestWorkflowEvents.RequestCancelled or SampleRequestWorkflowEvents.LabNote or
                Services.Workflow.SampleFlowRules.StandardPriceApproved;

    // A recorded business action may publish its system notice even when its actor cannot
    // chat in General. This never grants conversation membership or changes recipient scope.
    public static bool IsTrustedPublication(SampleRequestWorkflowEvent? evt, Guid requestId,
        Guid companyId, Guid actorId, bool actorIsActive)
        => evt is not null && evt.Id != Guid.Empty && actorId != Guid.Empty && companyId != Guid.Empty &&
            requestId != Guid.Empty && actorIsActive && evt.SampleRequestId == requestId &&
            evt.CompanyId == companyId && evt.RecordedByEmployeeId == actorId &&
            evt.EventType is SampleRequestWorkflowEvents.LabNote or SampleRequestWorkflowEvents.Created or SampleRequestWorkflowEvents.StandardReceived or
                SampleRequestWorkflowEvents.FormulaConfirmed or SampleRequestWorkflowEvents.PriceAccepted or
                SampleRequestWorkflowEvents.PriceRevoked or SampleRequestWorkflowEvents.SampleSent or
                SampleRequestWorkflowEvents.FeedbackRecorded or SampleRequestWorkflowEvents.RequestCancelled or
                Services.Workflow.SampleFlowRules.PriceFeedbackShared or Services.Workflow.SampleFlowRules.QuotationFinalized or Services.Workflow.SampleFlowRules.ChipFinalized or Services.Workflow.SampleFlowRules.ChipReopened or Services.Workflow.SampleFlowRules.KgRequested or
                Services.Workflow.SampleFlowRules.CompletedWithoutTrial or Services.Workflow.SampleFlowRules.PriceRejected or
                Services.Workflow.SampleFlowRules.PriceNoResponse or Services.Workflow.SampleFlowRules.ChipThenSampleRequested or Services.Workflow.SampleFlowRules.ChipRequested or
                Services.Workflow.SampleFlowRules.LabFinalizationRequested or Services.Workflow.SampleFlowRules.LowerPriceRequested or
                Services.Workflow.SampleFlowRules.PriceFollowUpDeferred or Services.Workflow.SampleFlowRules.ChipFinalizationRequested or
                Services.Workflow.SampleFlowRules.ChipFollowUpDeferred or Services.Workflow.SampleFlowRules.CompletionDeferred or
                Services.Workflow.SampleFlowRules.StandardPriceApproved or Services.Workflow.SampleRequestLabPauseRules.Rejected or Services.Workflow.SampleRequestLabPauseRules.Resumed or
                DeliveryOptions.RecordSampleBatchFeedbackCommandHandler.EventType;
}
