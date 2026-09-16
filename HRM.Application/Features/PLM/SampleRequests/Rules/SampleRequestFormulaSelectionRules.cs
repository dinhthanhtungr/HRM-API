using HRM.Application.Features.PLM.Shared.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestFormulaSelectionRules
{
    public static bool CanSelectFormula(
        bool isFormulaSelector,
        bool isLabUser,
        string? requestType,
        string? customerExternalId)
    {
        return isFormulaSelector ||
               (isLabUser &&
                SampleRequestMessageRules.IsInternalRequestType(requestType) &&
                PLMCustomerRules.IsInternalCustomerExternalId(customerExternalId));
    }
}
