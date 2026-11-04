using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestPriceConfirmationBasis
{
    public static Guid? ResolveFormulaId(SampleRequestSampleTrialReportDto item)
        => item.PriceConfirmationFormulaId ?? item.FormulaId;

    public static bool MatchesApprovedFormula(SampleRequestSampleTrialReportDto item, Guid? approvedSourceFormulaId)
        => ResolveFormulaId(item) is { } formulaId && formulaId == approvedSourceFormulaId;
}
