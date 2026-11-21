using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestPriceConfirmationBasis
{
    public static Guid? ResolveFormulaId(SampleRequestSampleTrialReportDto item)
        => item.PriceConfirmationFormulaId ?? item.FormulaId;

    public static bool HasApprovedPriceSource(Guid? approvedSourceFormulaId)
        // Price approval belongs to the request's product, independently of its sent option.
        => approvedSourceFormulaId.HasValue;
}
