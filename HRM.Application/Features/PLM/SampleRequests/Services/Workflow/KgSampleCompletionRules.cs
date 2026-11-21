using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class KgSampleCompletionRules
{
    internal static bool IsSatisfied(SampleRequestSampleTrial? latest, Guid? formulaId,
        IReadOnlyList<SampleRequestWorkflowEvent> events, SampleRequest? request = null)
        => latest is { IsActive: true, SentDate: not null, Status: SampleTrialStatus.Approved,
                DeliveryKind: SampleDeliveryKind.TrialSample } &&
            formulaId.HasValue && latest.FormulaId == formulaId &&
            (request != null && !RequiresPriceAcceptance(request, events) || SampleFlowStateReader.ActivePrice(events, formulaId));

    internal static bool RequiresPriceAcceptance(SampleRequest request, IReadOnlyList<SampleRequestWorkflowEvent> events)
        => Rules.SampleRequestCustomerPriceRules.IsQuotation(request.InfoType) || request.RealPriceQuoteDate.HasValue ||
            events.Any(e => e.EventType is SampleFlowRules.QuotationRequired or Rules.SampleRequestWorkflowEvents.PriceAccepted or Rules.SampleRequestWorkflowEvents.PriceRevoked or
                SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
}
