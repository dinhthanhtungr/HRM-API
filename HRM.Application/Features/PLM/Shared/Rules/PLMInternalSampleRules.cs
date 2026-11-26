using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.Shared.Rules;

/// <summary>Direct completion is narrower than notification suppression: both conditions are required.</summary>
internal static class PLMInternalSampleRules
{
    // SQL counterpart uses the persisted aliases, including decomposed Vietnamese text.
    internal static readonly string[] QueryKinds = ["noibo", "nộibộ", "nộibộ".Normalize(System.Text.NormalizationForm.FormD), "internal", "private"];
    internal static bool CanCompleteDirectly(string? requestType, string? customerExternalId)
        => SampleRequestMessageRules.IsInternalRequestType(requestType) &&
           PLMCustomerRules.IsInternalCustomerExternalId(customerExternalId);
}
